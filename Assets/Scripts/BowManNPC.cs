using UnityEngine;
using System.Collections;

public class BowManNPC : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 2.0f;
    [Tooltip("プレイヤーに近づかれたときに後退する速度")]
    public float retreatBackSpeed = 0.5f; // ★ 追いつきやすいようさらに遅めに調整
    
    public float detectRange = 8.0f;
    public float keepDistanceRange = 3.0f;
    public float attackRange = 5.0f;
    public float chargeTime = 0.5f;
    public float attackCooldown = 1.0f; // 射撃後の硬直時間

    [Tooltip("拠点（スポーン地点）からこれ以上離れて後退しない上限距離。足の遅い相手に追われ続けても永遠に逃げ続けないための歯止め")]
    public float leashRange = 10.0f;
    private Vector3 _homePosition;

    [Header("Home Base Settings")]
    [Tooltip("拠点となるオブジェクトの名前。見つかった場合、敵がいないときにこのオブジェクトの近くまで自動で戻る（リーシュ機能の基準点には影響しない）")]
    [SerializeField] private string homeBaseObjectName = "Bowyery Workshop";
    [Tooltip("敵がいないとき、拠点からこの距離以内まで近づいたら止まる")]
    [SerializeField] private float returnToBaseStopDistance = 2.0f;
    private Vector3 _returnBasePosition;
    
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
    
    // 退却設定
    [Header("Retreat Settings")]
    public float retreatMoveSpeed = 1.5f;
    public float retreatStopDistance = 0.5f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        rb.freezeRotation = true;

        _homePosition = transform.position;

        // 拠点復帰機能専用の目標地点（リーシュ機能の基準点には影響しない）
        _returnBasePosition = transform.position;
        GameObject homeBaseObj = GameObject.Find(homeBaseObjectName);
        if (homeBaseObj != null)
        {
            _returnBasePosition = homeBaseObj.transform.position;
        }

        // ArrowHitHandlerの取得または追加
        arrowHitHandler = GetComponent<ArrowHitHandler>();
        if (arrowHitHandler == null)
        {
            arrowHitHandler = gameObject.AddComponent<ArrowHitHandler>();
        }
        
        // 退却中の行動を制御するためのフラグをチェック
        isRetreating = arrowHitHandler.IsRetreating();

        // arrowSpawnPointが未設定の場合、自身のTransformを代用
        if (arrowSpawnPoint == null)
        {
            arrowSpawnPoint = transform;
        }
    }

    void Update()
    {
        // 退却中は他の処理をスキップ
        // 注意：実際の移動・アニメーション更新は ArrowHitHandler.RetreatToBlacksmith()
        // コルーチンが単独で担当している（rb.simulated=false にした上で
        // transform.position を直接動かしている）。
        // 以前はここで自前の ExecuteRetreatAI() も毎フレーム呼んでおり、
        // 同じ鍛冶屋座標へ向けて transform.position を「二重に」動かしてしまい、
        // 結果的に retreatMoveSpeed の実質2倍の速さで逃げてしまっていた
        // （＝プレイヤーが追いつけない一因）。ここでは状態の同期だけ行う。
        if (isRetreating)
        {
            if (arrowHitHandler != null)
            {
                isRetreating = arrowHitHandler.IsRetreating();
            }
            return;
        }
        
        FindTarget();
        
        // ターゲットがいない場合：拠点から離れていれば拠点付近まで自動で戻る
        if (target == null)
        {
            if (!isShooting)
            {
                ReturnToBase();
            }
            return;
        }

        // 射撃中の場合は何もしない
        if (isShooting) return;

        float distance = Vector2.Distance(transform.position, target.position);
        Vector2 moveDirection = Vector2.zero;
        float currentSpeed = moveSpeed; // 適用するスピード

        if (distance < keepDistanceRange && Vector2.Distance(transform.position, _homePosition) < leashRange)
        {
            // 近づかれすぎたので逃げる（後退速度を適用）
            // ただし拠点から leashRange 以上離れている場合は後退せず踏みとどまる
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
            // 攻撃射程内、または後退の上限（leashRange）に達した場合：
            // 停止して射撃開始（近づかれすぎていても、これ以上は下がらず応戦する）
            moveDirection = Vector2.zero;
            StartCoroutine(ShootRoutine());
        }

        Move(moveDirection, currentSpeed); // ★ スピードを引数に渡すように変更
        UpdateAnimation(moveDirection);
    }

    // 退却中の移動・アニメーションは ArrowHitHandler.RetreatToBlacksmith() が単独で担当する
    // （旧 ExecuteRetreatAI() / FindRetreatTarget() は二重移動の原因になっていたため削除済み）

    /// <summary>
    /// 索敵範囲内に敵がいないとき、拠点（Bowyery Workshop等）から離れていれば
    /// 拠点付近まで自動的に戻る。returnToBaseStopDistance以内まで近づいたら停止する。
    /// </summary>
    private void ReturnToBase()
    {
        float distanceFromHome = Vector2.Distance(transform.position, _returnBasePosition);

        if (distanceFromHome <= returnToBaseStopDistance)
        {
            // 既に拠点付近にいるので待機
            Move(Vector2.zero, 0f);
            UpdateAnimation(Vector2.zero);
            return;
        }

        Vector2 moveDirection = ((Vector2)_returnBasePosition - (Vector2)transform.position).normalized;
        Move(moveDirection, moveSpeed);
        UpdateAnimation(moveDirection);
    }

    private void FindTarget()
    {
        // 自身が"Ally"タグの場合（味方として生成されたBowManNPC）は、
        // Player/Allyではなく Ghost/Enemy を敵として索敵する（ThiefNPC/NPCPlayerHelperと同じ考え方）。
        // これをしないと、味方のはずのBowManNPCがプレイヤーや他の味方を撃ってしまう（同士討ち）。
        if (gameObject.CompareTag("Ally"))
        {
            FindHostileTarget();
            return;
        }

        // PlayerまたはAllyタグのオブジェクトを探す（敵側のBowManNPCの通常挙動）
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

            // 女性NPC（農作業などの非戦闘員）は攻撃対象から除外する
            FarmingNPC farmingData = ally.GetComponent<FarmingNPC>();
            if (farmingData != null && farmingData.gender == Gender.Female) continue;
            NPCPlayerHelper helperData = ally.GetComponent<NPCPlayerHelper>();
            if (helperData != null && helperData.gender == Gender.Female) continue;
            
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

    /// <summary>
    /// 味方（Allyタグ）として生成されたBowManNPCの索敵。
    /// ThiefNPC/NPCPlayerHelperと同じく、Ghost/Enemyタグを敵として狙う。
    /// </summary>
    private void FindHostileTarget()
    {
        GameObject closestTarget = null;
        float closestDistance = detectRange;

        GameObject[] ghosts = GameObject.FindGameObjectsWithTag("Ghost");
        foreach (GameObject g in ghosts)
        {
            CharacterHealth health = g.GetComponent<CharacterHealth>();
            if (health != null && health.isDead) continue;

            float distance = Vector2.Distance(transform.position, g.transform.position);
            if (distance <= detectRange && distance < closestDistance)
            {
                closestTarget = g;
                closestDistance = distance;
            }
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject e in enemies)
        {
            EnemyHealth enemyHealth = e.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.isDead) continue;

            float distance = Vector2.Distance(transform.position, e.transform.position);
            if (distance <= detectRange && distance < closestDistance)
            {
                closestTarget = e;
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