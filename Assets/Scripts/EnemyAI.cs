using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2.0f;

    [Header("AI Settings")]
    [SerializeField] private float detectRange = 5.0f;  
    [SerializeField] private float attackRange = 0.8f;  
    [SerializeField] private float attackCooldown = 1.5f; 

    private Transform currentTarget;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator; 
    private EnemyAxe enemyAxe; 
    private float attackTimer = 0f;
    private Vector2 moveDirection = Vector2.zero; // 物理移動用に方向を保持

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        
        // 物理移動のための設定を強制
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        
        // もし流用元のApply Root Motionがオンになっていたら強制オフにする（移動不能対策）
        if (animator != null) animator.applyRootMotion = false;

        SetEnemyNameUI();

        // プレイヤー用スクリプトが残っていないか警告するデバッグチェック
        if (GetComponent("PlayerController") != null)
        {
            Debug.LogError($"【要確認】{gameObject.name} に PlayerController が残っています！これが移動を邪魔している可能性が高いです。インスペクターから削除してください。");
        }

        enemyAxe = GetComponentInChildren<EnemyAxe>();
    }

    void Update()
    {
        if (attackTimer > 0) attackTimer -= Time.deltaTime;

        FindClosestTarget();

        if (currentTarget != null)
        {
            float distance = Vector2.Distance(transform.position, currentTarget.position);

            if (distance <= attackRange)
            {
                moveDirection = Vector2.zero;
                UpdateAnimation(Vector2.zero);
                TryAttack();
            }
            else
            {
                // 追跡方向を計算（FixedUpdate側で物理移動させる）
                moveDirection = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
                UpdateAnimation(moveDirection);

                if (spriteRenderer != null)
                {
                    if (moveDirection.x > 0.01f) spriteRenderer.flipX = false; // 右向き
                    else if (moveDirection.x < -0.01f) spriteRenderer.flipX = true; // 左向き
                }
            }
        }
        else
        {
            moveDirection = Vector2.zero;
            UpdateAnimation(Vector2.zero);
        }
    }

    // 2D物理移動は FixedUpdate で行うのがUnityの鉄則のため分離
    void FixedUpdate()
    {
        // 旧 velocity を使用して互換性を100%にする
        rb.linearVelocity = moveDirection * moveSpeed;
    }

    private void FindClosestTarget()
    {
        float closestDistance = detectRange; 
        Transform closestTransform = null;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            float dist = Vector2.Distance(transform.position, playerObj.transform.position);
            if (dist <= closestDistance) 
            {
                closestDistance = dist;
                closestTransform = playerObj.transform;
            }
        }

        FarmingNPC[] villagers = Object.FindObjectsByType<FarmingNPC>();
        foreach (var villager in villagers)
        {
            if (villager.gameObject.CompareTag("Player")) continue;
            float dist = Vector2.Distance(transform.position, villager.transform.position);
            if (dist <= closestDistance)
            {
                closestDistance = dist;
                closestTransform = villager.transform;
            }
        }

        ThiefNPC[] thieves = Object.FindObjectsByType<ThiefNPC>();
        foreach (var thief in thieves)
        {
            if (thief.gameObject.CompareTag("Player")) continue;
            float dist = Vector2.Distance(transform.position, thief.transform.position);
            if (dist <= closestDistance)
            {
                closestDistance = dist;
                closestTransform = thief.transform;
            }
        }

        currentTarget = closestTransform;
    }

    private void UpdateAnimation(Vector2 moveInput)
    {
        if (animator == null) return;
        float speed = moveInput.sqrMagnitude;
        animator.SetFloat("Speed", speed); 
    }

    private void TryAttack()
    {
        if (attackTimer > 0) return;

        if (enemyAxe != null)
        {
            enemyAxe.ExecuteAttack();
        }
        
        attackTimer = attackCooldown;
    }

    private void SetEnemyNameUI()
    {
        TextMeshPro tmp = GetComponentInChildren<TextMeshPro>();
        if (tmp != null)
        {
            tmp.text = "Enemy";
            // 敵だと分かりやすいように、必要であれば色を赤っぽくすることも可能です
            tmp.color = Color.red; 
        }
        else
        {
            // TextMeshProUGUI（Canvasベース）を使っている場合のフォールバック
            TextMeshProUGUI tmpUI = GetComponentInChildren<TextMeshProUGUI>();
            if (tmpUI != null)
            {
                tmpUI.text = "Enemy";
                tmpUI.color = Color.red;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}