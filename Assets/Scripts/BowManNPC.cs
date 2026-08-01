using UnityEngine;
using System.Collections;

public class BowManNPC : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 2.0f;
    [Tooltip("プレイヤーに近づかれたときに後退する速度")]
    public float retreatBackSpeed = 1.0f; // ★ ここを追加（デフォルトで遅めに設定）
    
    public float detectRange = 8.0f;
    public float keepDistanceRange = 3.0f;
    public float attackRange = 5.0f;
    public float chargeTime = 0.5f;
    public float attackCooldown = 1.0f; // 射撃後の硬直時間
    
    [Header("References")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform arrowSpawnPoint;
    
    private Transform target;
    private Animator animator;
    private Rigidbody2D rb;
    private bool isShooting = false;
    
    // 矢の被弾処理用コンポーネント
    private ArrowHitHandler arrowHitHandler;
    
    // 退却中フラグ
    private bool isRetreating = false;
    // 退却先
    private Transform retreatTarget;
    
    // 退却設定
    [Header("Retreat Settings")]
    public float retreatMoveSpeed = 1.5f;
    public float retreatStopDistance = 0.5f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        rb.freezeRotation = true;

        // ArrowHitHandlerの取得または追加
        arrowHitHandler = GetComponent<ArrowHitHandler>();
        if (arrowHitHandler == null)
        {
            arrowHitHandler = gameObject.AddComponent<ArrowHitHandler>();
        }
        
        // 退却中の行動を制御するためのフラグをチェック
        isRetreating = arrowHitHandler.IsRetreating();
        
        // 退却先を取得
        FindRetreatTarget();

        // arrowSpawnPointが未設定の場合、自身のTransformを代用
        if (arrowSpawnPoint == null)
        {
            arrowSpawnPoint = transform;
        }
    }

    void Update()
    {
        // 退却中は他の処理をスキップ
        if (isRetreating)
        {
            if (arrowHitHandler != null)
            {
                isRetreating = arrowHitHandler.IsRetreating();
            }
            ExecuteRetreatAI();
            return;
        }
        
        FindTarget();
        
        // ターゲットがいない、または射撃中の場合は何もしない
        if (target == null || isShooting) return;

        float distance = Vector2.Distance(transform.position, target.position);
        Vector2 moveDirection = Vector2.zero;
        float currentSpeed = moveSpeed; // 適用するスピード

        if (distance < keepDistanceRange)
        {
            // 近づかれすぎたので逃げる（後退速度を適用）
            moveDirection = (transform.position - target.position).normalized;
            currentSpeed = retreatBackSpeed; // ★ ここで遅い後退速度に切り替える
        }
        else if (distance > attackRange)
        {
            // 遠いので追いかける
            moveDirection = (target.position - transform.position).normalized;
            currentSpeed = moveSpeed;
        }
        else
        {
            // 攻撃射程内：停止して射撃開始
            moveDirection = Vector2.zero;
            StartCoroutine(ShootRoutine());
        }

        Move(moveDirection, currentSpeed); // ★ スピードを引数に渡すように変更
        UpdateAnimation(moveDirection);
    }

    /// <summary>
    /// 退却中のAI処理
    /// </summary>
    private void ExecuteRetreatAI()
    {
        if (retreatTarget == null)
        {
            FindRetreatTarget();
            if (retreatTarget == null)
            {
                isRetreating = false;
                return;
            }
        }

        float distanceToTarget = Vector2.Distance(transform.position, retreatTarget.position);

        // 退却先に到達したら退却完了
        if (distanceToTarget <= retreatStopDistance)
        {
            isRetreating = false;
            if (animator != null) UpdateAnimation(Vector2.zero);
            return;
        }

        Vector3 moveDirection = ((Vector3)retreatTarget.position - transform.position).normalized;
        Vector3 nextPosition = Vector2.MoveTowards(transform.position, retreatTarget.position, retreatMoveSpeed * Time.deltaTime);
        transform.position = nextPosition;

        if (animator != null)
        {
            UpdateAnimation(moveDirection);
        }
    }

    /// <summary>
    /// 退却先を検索
    /// </summary>
    private void FindRetreatTarget()
    {
        // ArrowHitHandlerからBlacksmithの位置を取得
        if (arrowHitHandler != null && arrowHitHandler.blacksmithPosition != null)
        {
            retreatTarget = arrowHitHandler.blacksmithPosition;
            return;
        }
        
        // シーン内からBlacksmithタグのオブジェクトを検索
        GameObject blacksmithObj = GameObject.FindGameObjectWithTag("Blacksmith");
        if (blacksmithObj != null)
        {
            retreatTarget = blacksmithObj.transform;
        }
    }

    private void FindTarget()
    {
        // PlayerまたはAllyタグのオブジェクトを探す
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject[] allies = GameObject.FindGameObjectsWithTag("Ally");
        
        GameObject closestTarget = null;
        float closestDistance = detectRange;
        
        // Playerを優先的にチェック
        if (player != null && Vector2.Distance(transform.position, player.transform.position) <= detectRange)
        {
            CharacterHealth health = player.GetComponentInParent<CharacterHealth>();
            if (health != null && health.isDead)
            {
                player = null;
            }
            else
            {
                closestTarget = player;
                closestDistance = Vector2.Distance(transform.position, player.transform.position);
            }
        }
        
        // AllyタグのNPCをチェック（Playerより遠い場合のみ）
        foreach (GameObject ally in allies)
        {
            // 自分自身は除外
            if (ally == gameObject) continue;
            
            float distance = Vector2.Distance(transform.position, ally.transform.position);
            if (distance <= detectRange && distance < closestDistance)
            {
                CharacterHealth health = ally.GetComponentInParent<CharacterHealth>();
                if (health != null && health.isDead) continue;
                
                closestTarget = ally;
                closestDistance = distance;
            }
        }
        
        target = closestTarget != null ? closestTarget.transform : null;
    }

    private void Move(Vector2 direction, float speed)
    {
        rb.linearVelocity = direction * speed; // ★ 指定されたスピードで移動
    }

    private void UpdateAnimation(Vector2 moveInput)
    {
        if (animator == null) return;

        if (moveInput.magnitude > 0.1f)
        {
            animator.SetFloat("LastInputX", moveInput.x);
            animator.SetFloat("LastInputY", moveInput.y);
        }

        animator.SetFloat("InputX", moveInput.x);
        animator.SetFloat("InputY", moveInput.y);
        animator.SetFloat("Speed", moveInput.magnitude);
    }

    private IEnumerator ShootRoutine()
    {
        isShooting = true;
        rb.linearVelocity = Vector2.zero;
        
        // 射撃準備：アニメーション移動パラメータを停止状態にする
        if (animator != null)
        {
            animator.SetFloat("InputX", 0);
            animator.SetFloat("InputY", 0);
            animator.SetFloat("Speed", 0);

            // ターゲットの方を向く（LastInputX/Y を更新）
            if (target != null)
            {
                Vector2 dirToTarget = (target.position - transform.position).normalized;
                animator.SetFloat("LastInputX", dirToTarget.x);
                animator.SetFloat("LastInputY", dirToTarget.y);
            }
        }

        // 溜めアニメーション発動
        if (animator != null) animator.SetTrigger("Shooting_Before");
        yield return new WaitForSeconds(chargeTime);
        
        // 溜め完了後に矢を発射
        if (target != null)
        {
            if (animator != null) animator.SetTrigger("Shooting_After");
            SpawnArrow();
        }

        yield return new WaitForSeconds(attackCooldown); // 射撃後のクールタイム
        isShooting = false;
    }

    private void SpawnArrow()
    {
        if (arrowPrefab != null && arrowSpawnPoint != null && target != null)
        {
            GameObject arrowObj = Instantiate(arrowPrefab, arrowSpawnPoint.position, Quaternion.identity);
            Arrow arrow = arrowObj.GetComponent<Arrow>();
            if (arrow != null)
            {
                Vector2 dir = (target.position - arrowSpawnPoint.position).normalized;
                arrow.SetDirection(dir);
            }
        }
    }

    // デバッグ用に検知範囲・攻撃範囲をシーンビューに描画
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, keepDistanceRange);
    }
}