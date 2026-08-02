using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class ThiefNPC : FarmingNPC
{
    [Header("Player Sync Settings (Integrated)")]
    [SerializeField] private float syncMoveSpeed = 5f;
    private Vector2 syncMoveInput;

    [Header("Mining Settings")]
    public float miningTime = 1.0f;
    public GameObject ironOrePrefab;
    public TileBase afterMinedTile;

    [Header("Combat Settings")]
    [Tooltip("通常時（自衛）の索敵範囲")]
    public float normalDetectionRange = 5.0f;
    [Tooltip("攻撃指示（コマンド）実行時の広範囲索敵")]
    public float commandedDetectionRange = 15.0f;
    public float attackRange = 1.5f;
    public float combatMoveSpeed = 2.0f;

    [Header("Assembly / Gather Settings")]
    [Tooltip("コマンド実行時に敵がいない場合、プレイヤーに集合するときの移動速度")]
    public float gatherMoveSpeed = 4.0f;
    [Tooltip("プレイヤーの周囲に到着したとみなす距離")]
    public float gatherStopDistance = 1.5f;

    [Header("Retreat Settings")]
    [Tooltip("退却時の移動速度")]
    public float retreatMoveSpeed = 1.5f;
    [Tooltip("退却完了の判定距離")]
    public float retreatStopDistance = 0.5f;

    [Header("Combat References")]
    [SerializeField] private Hammer hammerComponent;

    [Header("Command Key Settings")]
    [Tooltip("味方NPCへの攻撃指示キー（NPCSpawnerのinteractKeyと重複しないこと）")]
    [SerializeField] private KeyCode attackCommandKey = KeyCode.Q;
    [Tooltip("攻撃指示のキャンセルキー")]
    [SerializeField] private KeyCode cancelCommandKey = KeyCode.X;

    private bool isMining = false;
    private Vector3Int minedGridPos;
    private Transform targetGhost;
    private CharacterHealth health;
    private float attackCooldown = 1.2f;
    private float nextAttackTime = 0f;

    // 攻撃指示フラグ
    private bool isCommandedToAttack = false;
    // 敵がいなかったためプレイヤーに集合中かどうかのフラグ
    private bool isGatheringToPlayer = false;
    // プレイヤーのTransform参照
    private Transform playerTransform;
    
    // 退却中フラグ
    private bool isRetreating = false;

    private bool IsActingIndependently => isMining || targetGhost != null || isGatheringToPlayer || isRetreating; 

    protected override void Awake()
    {
        base.Awake();

        // FarmingNPC.Awake() applies linearDamping = 10 for the (position-driven)
        // watering NPCs. ThiefNPC moves via rb.linearVelocity directly (sync/combat/
        // gather/retreat), so that drag would fight the assigned velocity every
        // physics step and make movement noticeably slower than moveSpeed implies.
        Rigidbody2D thiefRb = GetComponent<Rigidbody2D>();
        if (thiefRb != null)
        {
            thiefRb.linearDamping = 0f;
        }

        if (!gameObject.CompareTag("Player"))
        {
            gameObject.tag = "Ally";
        }

        health = GetComponent<CharacterHealth>();

        if (hammerComponent == null)
        {
            hammerComponent = GetComponentInChildren<Hammer>();
            if (hammerComponent != null)
            {
                hammerComponent.ChangeToTuruhashi();
            }
        }
        
        // 退却中の行動を制御するためのフラグをチェック
        if (arrowHitHandler != null)
        {
            isRetreating = arrowHitHandler.IsRetreating();
        }
    }

    protected override void Start()
    {
        base.Start();
        FindPlayerTransform();
    }

    void Update()
    {
        if (health != null && health.isDead) return;
        
        // 退却中は他の処理をスキップ
        // 注意：実際の移動・アニメーション更新は ArrowHitHandler.RetreatToBlacksmith()
        // コルーチンが単独で担当している。以前はここでも自前の ExecuteRetreatAI() を
        // 毎フレーム呼んでおり、同じ鍛冶屋座標へ向けて transform.position を二重に
        // 動かしてしまっていた（＝実質2倍速で退却していた）ため削除した。
        if (isRetreating)
        {
            if (arrowHitHandler != null)
            {
                isRetreating = arrowHitHandler.IsRetreating();
            }
            return;
        }

        if (Input.GetKeyDown(attackCommandKey))
        {
            CommandAttack();
        }

        if (Input.GetKeyDown(cancelCommandKey))
        {
            CancelAttackCommand();
        }

        FindGhost();

        // 1. 敵を発見した場合（戦闘優先）
        if (targetGhost != null)
        {
            isGatheringToPlayer = false; // 戦闘に入るので集合は解除
            if (isMining)
            {
                StopAllCoroutines();
                isMining = false;
            }
            ExecuteCombatAI();
        }
        // 2. 攻撃コマンド中だが敵が見つからず、プレイヤーに集合中の場合
        else if (isGatheringToPlayer)
        {
            if (isMining)
            {
                StopAllCoroutines();
                isMining = false;
            }
            ExecuteGatheringAI();
        }
        else if (isMining)
        {
            syncMoveInput = Vector2.zero;
        }
        else
        {
            // 索敵範囲内に敵がいなくなったら自動で攻撃指示モードをオフにして追従へ戻る
            if (isCommandedToAttack)
            {
                isCommandedToAttack = false;
            }
            GetPlayerInputSync();
        }
    }

    void FixedUpdate()
    {
        if ((health != null && health.isDead) || IsActingIndependently) return;

        Rigidbody2D targetRb = GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            targetRb.linearVelocity = syncMoveInput * syncMoveSpeed;
        }
    }

    // 退却中の移動・アニメーションは ArrowHitHandler.RetreatToBlacksmith() が単独で担当する
    // （旧 ExecuteRetreatAI() / FindRetreatTarget() は二重移動の原因になっていたため削除済み）

    private void ExecuteCombatAI()
    {
        if (targetGhost == null) return;

        float distance = Vector2.Distance(transform.position, targetGhost.position);
        Vector3 moveDirection = ((Vector3)targetGhost.position - transform.position).normalized;

        Rigidbody2D targetRb = GetComponent<Rigidbody2D>();

        if (distance > attackRange)
        {
            if (targetRb != null) targetRb.linearVelocity = Vector2.zero;
            Vector3 nextPosition = Vector2.MoveTowards(transform.position, targetGhost.position, combatMoveSpeed * Time.deltaTime);
            transform.position = nextPosition;

            if (animator != null)
            {
                UpdateAnimatorFloat("InputX", moveDirection.x);
                UpdateAnimatorFloat("InputY", moveDirection.y);
                UpdateAnimatorFloat("Speed", combatMoveSpeed);
            }
        }
        else
        {
            if (targetRb != null) targetRb.linearVelocity = Vector2.zero;
            if (animator != null) UpdateAnimatorFloat("Speed", 0f);

            if (Time.time >= nextAttackTime)
            {
                if (hammerComponent != null)
                {
                    hammerComponent.ExecuteAttack();
                    nextAttackTime = Time.time + attackCooldown;
                }
            }
        }
    }

    /// <summary>
    /// 敵が見つからなかった場合にプレイヤーの元へ駆け寄るAI処理
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

        // プレイヤーの近く（gatherStopDistance以内）に到達したら集合状態を解除する
        if (distanceToPlayer <= gatherStopDistance)
        {
            isGatheringToPlayer = false;
            isCommandedToAttack = false;
            if (animator != null) UpdateAnimatorFloat("Speed", 0f);
            return;
        }

        Rigidbody2D targetRb = GetComponent<Rigidbody2D>();
        if (targetRb != null) targetRb.linearVelocity = Vector2.zero;

        Vector3 moveDirection = ((Vector3)playerTransform.position - transform.position).normalized;
        Vector3 nextPosition = Vector2.MoveTowards(transform.position, playerTransform.position, gatherMoveSpeed * Time.deltaTime);
        transform.position = nextPosition;

        if (animator != null)
        {
            UpdateAnimatorFloat("InputX", moveDirection.x);
            UpdateAnimatorFloat("InputY", moveDirection.y);
            UpdateAnimatorFloat("Speed", gatherMoveSpeed);
        }
    }

    private void FindGhost()
    {
        float currentDetectionRange = isCommandedToAttack ? commandedDetectionRange : normalDetectionRange;

        float minDistance = currentDetectionRange;
        targetGhost = null;

        GameObject[] ghosts = GameObject.FindGameObjectsWithTag("Ghost");
        foreach (GameObject g in ghosts)
        {
            var ghostHealth = g.GetComponent<CharacterHealth>();
            if (ghostHealth != null && ghostHealth.isDead) continue;
            float dist = Vector2.Distance(transform.position, g.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                targetGhost = g.transform;
            }
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject e in enemies)
        {
            var enemyHealth = e.GetComponent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.isDead) continue;
            float dist = Vector2.Distance(transform.position, e.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                targetGhost = e.transform;
            }
        }
    }

    /// <summary>
    /// 攻撃指示を出すメソッド
    /// </summary>
    public void CommandAttack()
    {
        isCommandedToAttack = true;
        
        // コマンド実行時に一度索敵を行って敵を探す
        FindGhost();

        // 敵が見つからなかった場合、プレイヤーに集合するモードをONにする
        if (targetGhost == null)
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
    /// 攻撃指示をキャンセルし、通常範囲へ復帰させるメソッド
    /// </summary>
    public void CancelAttackCommand()
    {
        isCommandedToAttack = false;
        isGatheringToPlayer = false;
        targetGhost = null;
    }

    private void FindPlayerTransform()
    {
        // "Player" タグがついているオブジェクトを操作中のプレイヤーとして探す
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    public void AssignMiningTask(Tilemap tilemap, Vector3Int gridPos, TileBase tileBase, bool canMine)
    {
        if (targetGhost != null || isMining || isGatheringToPlayer || isRetreating || !canMine) return;

        targetTilemap = tilemap;
        targetGridPos = gridPos;
        minedGridPos = gridPos;
        StartCoroutine(DoMiningWork(tilemap));
    }

    private IEnumerator DoMiningWork(Tilemap tilemap)
    {
        isMining = true;
        Rigidbody2D targetRb = GetComponent<Rigidbody2D>();
        if (targetRb != null) targetRb.linearVelocity = Vector2.zero;

        Vector3 targetWorldPos = tilemap.GetCellCenterWorld(minedGridPos);
        while (Vector3.Distance(transform.position, targetWorldPos) > 0.05f)
        {
            if (targetGhost != null || isGatheringToPlayer || isRetreating)
            {
                isMining = false;
                yield break;
            }

            Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
            Vector3 moveDirection = (nextPosition - transform.position).normalized;

            if (animator != null)
            {
                UpdateAnimatorFloat("InputX", moveDirection.x);
                UpdateAnimatorFloat("InputY", moveDirection.y);
                UpdateAnimatorFloat("Speed", moveDirection.magnitude);
            }

            transform.position = nextPosition;
            yield return null;
        }

        if (animator != null) UpdateAnimatorFloat("Speed", 0f);

        if (hammerComponent != null)
        {
            yield return StartCoroutine(hammerComponent.SwingAndSpinHammer());
        }
        else
        {
            yield return new WaitForSeconds(miningTime);
        }

        if (ironOrePrefab != null)
        {
            Instantiate(ironOrePrefab, tilemap.GetCellCenterWorld(minedGridPos), Quaternion.identity);
        }

        if (ResourceUIManager.Instance != null)
        {
            ResourceUIManager.Instance.AddIron(1);
        }

        if (afterMinedTile != null)
        {
            tilemap.SetTile(minedGridPos, afterMinedTile);
        }
        else
        {
            tilemap.SetTile(minedGridPos, null);
        }

        isMining = false;
    }

    private void GetPlayerInputSync()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        syncMoveInput = new Vector2(x, y).normalized;

        if (animator != null)
        {
            UpdateAnimatorFloat("InputX", x);
            UpdateAnimatorFloat("InputY", y);
            UpdateAnimatorFloat("Speed", syncMoveInput.magnitude);

            if (syncMoveInput.magnitude > 0.1f)
            {
                UpdateAnimatorFloat("LastInputX", x);
                UpdateAnimatorFloat("LastInputY", y);
            }
        }
    }
}