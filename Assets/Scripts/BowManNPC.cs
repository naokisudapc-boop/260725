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

    [Header("Manual Aiming (操作キャラクターになったとき用)")]
    [Tooltip("マウスドラッグ中に表示する予測射線の長さ")]
    [SerializeField] private float aimLineLength = 6f;
    [Tooltip("射線プレビュー用のLineRenderer。未設定なら自動生成する")]
    [SerializeField] private LineRenderer aimLineRenderer;

    private CharacterHealth _characterHealth;
    private bool _isAiming = false;
    private Vector2 _aimDirection = Vector2.right;
    
    // 矢の被弾処理用コンポーネント
    private ArrowHitHandler arrowHitHandler;
    
    // 退却中フラグ
    private bool isRetreating = false;
    
    // 退却設定
    [Header("Retreat Settings")]
    public float retreatMoveSpeed = 1.5f;
    public float retreatStopDistance = 0.5f;

    [Header("Ally Command Settings（Allyタグのときのみ使用）")]
    [Tooltip("味方への攻撃指示キー（ThiefNPC/NPCPlayerHelperと同じQキーを共有）")]
    [SerializeField] private KeyCode attackCommandKey = KeyCode.Q;
    [SerializeField] private KeyCode cancelCommandKey = KeyCode.X;
    [Tooltip("操作キャラクターの入力に同期して移動する速度")]
    [SerializeField] private float syncMoveSpeed = 3f;
    [Tooltip("攻撃指示で敵が見つからないとき、操作キャラクターへ集合する際の移動速度")]
    [SerializeField] private float gatherMoveSpeed = 4f;
    [Tooltip("集合の際、操作キャラクターからこの距離以内まで近づいたら停止する")]
    [SerializeField] private float gatherStopDistance = 2f;

    private Vector2 syncMoveInput;
    private Transform playerTransform;
    private bool isCommandedToAttack = false;
    private bool isGatheringToPlayer = false;

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

        // 味方（Allyタグ）の場合のみ、GameManagerの操作キャラクター交代システムが
        // 候補として見つけられるよう CharacterHealth を用意する。
        // 敵（Enemyタグ）は従来通り EnemyHealth のみを使用し、CharacterHealth は追加しない。
        if (gameObject.CompareTag("Ally"))
        {
            EnsureCharacterHealthForAlly();
            EnsureAimLineRenderer();
        }
    }

    /// <summary>
    /// 味方BowManNPCがGameManagerの操作キャラクター候補（CharacterHealth検索）に
    /// 含まれるよう、CharacterHealthを自動的にアタッチ・有効化する。
    /// ThiefNPC/NPCPlayerHelperと同じ仕組みで操作キャラクター交代できるようにするため。
    /// </summary>
    private void EnsureCharacterHealthForAlly()
    {
        _characterHealth = GetComponent<CharacterHealth>();
        if (_characterHealth == null)
        {
            _characterHealth = gameObject.AddComponent<CharacterHealth>();
        }
        _characterHealth.isPlayer = false;
        _characterHealth.isControllable = false;
        _characterHealth.enabled = true;
    }

    /// <summary>
    /// 矢の予測射線を表示するためのLineRendererを用意する（未設定なら自動生成）。
    /// </summary>
    private void EnsureAimLineRenderer()
    {
        if (aimLineRenderer != null) return;

        GameObject lineObj = new GameObject("AimLine");
        lineObj.transform.SetParent(transform, false);
        aimLineRenderer = lineObj.AddComponent<LineRenderer>();
        aimLineRenderer.positionCount = 2;
        aimLineRenderer.startWidth = 0.05f;
        aimLineRenderer.endWidth = 0.05f;
        aimLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        aimLineRenderer.startColor = new Color(1f, 1f, 1f, 0.8f);
        aimLineRenderer.endColor = new Color(1f, 1f, 1f, 0.2f);
        aimLineRenderer.sortingOrder = 100;
        aimLineRenderer.enabled = false;
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

        if (_characterHealth != null && _characterHealth.isPlayer)
        {
            UpdateAsControlledPlayer();
        }
        else if (gameObject.CompareTag("Ally"))
        {
            UpdateAsAlly();
        }
        else
        {
            UpdateAsEnemy();
        }
    }

    /// <summary>
    /// 操作キャラクターになっているときの挙動。移動は NewMonoBehaviourScript が担当するので、
    /// ここではマウスドラッグでの照準・矢の発射のみを扱う。
    /// 左クリック押下でドラッグ開始、押している間は予測射線を表示、離した瞬間に発射する。
    /// </summary>
    private void UpdateAsControlledPlayer()
    {
        if (Input.GetMouseButtonDown(0) && !isShooting)
        {
            _isAiming = true;
            if (aimLineRenderer != null) aimLineRenderer.enabled = true;
        }

        if (_isAiming && Input.GetMouseButton(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = arrowSpawnPoint.position.z;

            Vector2 dir = ((Vector2)mouseWorldPos - (Vector2)arrowSpawnPoint.position);
            if (dir.sqrMagnitude > 0.0001f)
            {
                _aimDirection = dir.normalized;
            }

            // アニメーションの向きを照準方向に合わせる
            if (animator != null)
            {
                animator.SetFloat("InputX", 0);
                animator.SetFloat("InputY", 0);
                animator.SetFloat("Speed", 0);
                animator.SetFloat("LastInputX", _aimDirection.x);
                animator.SetFloat("LastInputY", _aimDirection.y);
            }

            // 予測射線を更新
            if (aimLineRenderer != null)
            {
                aimLineRenderer.SetPosition(0, arrowSpawnPoint.position);
                aimLineRenderer.SetPosition(1, arrowSpawnPoint.position + (Vector3)(_aimDirection * aimLineLength));
            }
        }

        if (_isAiming && Input.GetMouseButtonUp(0))
        {
            _isAiming = false;
            if (aimLineRenderer != null) aimLineRenderer.enabled = false;

            if (!isShooting)
            {
                StartCoroutine(ManualShootRoutine(_aimDirection));
            }
        }
    }

    /// <summary>
    /// マウスドラッグで指定した方向へ矢を発射する（操作キャラクター用）。
    /// AIのShootRoutine()と違い、targetではなく明示的な方向を使う。
    /// </summary>
    private IEnumerator ManualShootRoutine(Vector2 direction)
    {
        isShooting = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetTrigger("Shooting_Before");
        }

        yield return new WaitForSeconds(chargeTime);

        if (animator != null) animator.SetTrigger("Shooting_After");
        SpawnArrow(direction);

        yield return new WaitForSeconds(attackCooldown);
        isShooting = false;
    }

    /// <summary>
    /// 敵（Enemyタグ）としての通常挙動。従来のロジックのまま。
    /// </summary>
    private void UpdateAsEnemy()
    {
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

        ExecuteTargetedMovement();
    }

    /// <summary>
    /// 味方（Allyタグ）としての挙動。ThiefNPC/NPCPlayerHelperと同じく、
    /// Qキーでの攻撃指示・敵不在時のプレイヤーへの集合・平常時のプレイヤー入力同期を行う。
    /// </summary>
    private void UpdateAsAlly()
    {
        if (Input.GetKeyDown(attackCommandKey))
        {
            CommandAttack();
        }

        if (Input.GetKeyDown(cancelCommandKey))
        {
            CancelAttackCommand();
        }

        FindHostileTarget();

        // 1. 敵を発見した場合（戦闘優先）
        if (target != null)
        {
            isGatheringToPlayer = false; // 戦闘に入るので集合は解除
            if (isShooting) return;
            ExecuteTargetedMovement();
            return;
        }

        if (isShooting) return;

        // 2. 攻撃コマンド中だが敵が見つからず、プレイヤーに集合中の場合
        if (isGatheringToPlayer)
        {
            ExecuteGatheringAI();
            return;
        }

        // 3. 索敵範囲内に敵がいなくなったら自動で攻撃指示モードをオフにして追従へ戻る
        if (isCommandedToAttack)
        {
            isCommandedToAttack = false;
        }

        SyncMoveWithPlayer();
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

    /// <summary>
    /// target に向けた距離ベースの移動判断（近すぎれば後退、遠ければ追跡、射程内なら射撃）。
    /// 敵・味方どちらのBowManNPCも共通で使用する。
    /// </summary>
    private void ExecuteTargetedMovement()
    {
        float distance = Vector2.Distance(transform.position, target.position);
        Vector2 moveDirection = Vector2.zero;
        float currentSpeed = moveSpeed;

        if (distance < keepDistanceRange && Vector2.Distance(transform.position, _homePosition) < leashRange)
        {
            // 近づかれすぎたので逃げる（後退速度を適用）
            // ただし拠点から leashRange 以上離れている場合は後退せず踏みとどまる
            moveDirection = (transform.position - target.position).normalized;
            currentSpeed = retreatBackSpeed;
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

        Move(moveDirection, currentSpeed);
        UpdateAnimation(moveDirection);
    }

    /// <summary>
    /// Qキーで攻撃指示を出す。ThiefNPC.CommandAttack()と同じ考え方。
    /// 即座に索敵し、敵が見つからなければ操作キャラクターへ集合する。
    /// </summary>
    public void CommandAttack()
    {
        isCommandedToAttack = true;

        FindHostileTarget();

        if (target == null)
        {
            FindPlayerTransform();
            if (playerTransform != null)
            {
                isGatheringToPlayer = true;
            }
        }
        else
        {
            isGatheringToPlayer = false;
        }
    }

    /// <summary>
    /// 攻撃指示をキャンセルする。
    /// </summary>
    public void CancelAttackCommand()
    {
        isCommandedToAttack = false;
        isGatheringToPlayer = false;
        target = null;
    }

    private void FindPlayerTransform()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    /// <summary>
    /// 攻撃指示中に敵が見つからない場合、操作キャラクターへ集合する。
    /// gatherStopDistance以内まで近づいたら停止する（ThiefNPC.ExecuteGatheringAI()と同じ考え方）。
    /// </summary>
    private void ExecuteGatheringAI()
    {
        if (playerTransform == null)
        {
            FindPlayerTransform();
            if (playerTransform == null)
            {
                isGatheringToPlayer = false;
                return;
            }
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= gatherStopDistance)
        {
            isGatheringToPlayer = false;
            isCommandedToAttack = false;
            Move(Vector2.zero, 0f);
            UpdateAnimation(Vector2.zero);
            return;
        }

        Vector2 moveDirection = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        Move(moveDirection, gatherMoveSpeed);
        UpdateAnimation(moveDirection);
    }

    /// <summary>
    /// 敵がおらず攻撃指示もないとき、操作キャラクターの移動入力に同期して移動する
    /// （ThiefNPC.GetPlayerInputSync()と同じ考え方）。
    /// </summary>
    private void SyncMoveWithPlayer()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        syncMoveInput = new Vector2(x, y).normalized;

        Move(syncMoveInput, syncMoveSpeed);
        UpdateAnimation(syncMoveInput);
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

    private void SpawnArrow(Vector2? overrideDirection = null)
    {
        if (arrowPrefab == null || arrowSpawnPoint == null) return;

        Vector2 dir;
        if (overrideDirection.HasValue)
        {
            dir = overrideDirection.Value;
        }
        else if (target != null)
        {
            dir = (target.position - arrowSpawnPoint.position).normalized;
        }
        else
        {
            return;
        }

        GameObject arrowObj = Instantiate(arrowPrefab, arrowSpawnPoint.position, Quaternion.identity);
        Arrow arrow = arrowObj.GetComponent<Arrow>();
        if (arrow != null)
        {
            arrow.SetDirection(dir);
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