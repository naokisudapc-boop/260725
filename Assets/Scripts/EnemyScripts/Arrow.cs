using UnityEngine;

public class Arrow : MonoBehaviour
{
    private float speed = 15f;
    private Vector2 direction;
    private bool isHit = false;
    

    private Rigidbody2D rb;
    
    // 射手を追跡して自己衝突を防ぐ
    private BowManNPC shooter;

    [Header("Lifetime Settings")]
    [Tooltip("矢の滞空時間（秒）。命中したかどうかに関わらず、この時間が経過すると自動的に消える")]
    [SerializeField] private float lifeTime = 2f;

    [Header("Penetration Settings")]
    [Tooltip("命中した瞬間の位置から、矢の向きにさらに進める距離。値を大きくするほど深く突き刺さり、先端がスプライトを貫通して見える")]
    [SerializeField] private float penetrationDepth = 0.3f;

    [Header("Headshot Settings")]
    [Tooltip("命中位置が対象のコライダー上部からこの割合以内なら頭部命中（即死）とみなす")]
    [Range(0f, 1f)]
    [SerializeField] private float headshotZoneRatio = 0.25f;

    /// <summary>
    /// 命中位置が対象コライダーの「頭部エリア」（上部headshotZoneRatio）に入っているか判定する
    /// </summary>
    private bool IsHeadshot(Collider2D targetCollider)
    {
        if (targetCollider == null) return false;

        Bounds bounds = targetCollider.bounds;
        if (bounds.size.y <= 0f) return false;

        float headZoneStart = bounds.max.y - bounds.size.y * headshotZoneRatio;
        return transform.position.y >= headZoneStart;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // 滞空時間が経過しても何にも命中しなかった場合、消すのではなく地面に刺さる
        if (lifeTime > 0f)
        {
            Invoke(nameof(StickToGround), lifeTime);
        }
    }

    /// <summary>
    /// 滞空時間が経過しても何にも命中しなかった場合に呼ばれる。矢を地面に刺さった状態にする。
    /// </summary>
    private void StickToGround()
    {
        if (isHit) return; // 既に何かに命中している場合は何もしない

        isHit = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        // 進行方向にわずかに埋め込むことで、地面に突き刺さって見えるようにする
        transform.position += (Vector3)(direction * penetrationDepth);
    }

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
    
    /// <summary>
    /// 射手を設定し、自己衝突を防ぐためのCollider間の衝突を無視する
    /// </summary>
    public void SetShooter(BowManNPC shooter)
    {
        this.shooter = shooter;
        
        // 射手のColliderとArrowのCollider間の衝突を無視
        if (shooter != null)
        {
            Collider2D arrowCollider = GetComponent<Collider2D>();
            Collider2D shooterCollider = shooter.GetComponent<Collider2D>();
            
            if (arrowCollider != null && shooterCollider != null)
            {
                Physics2D.IgnoreCollision(arrowCollider, shooterCollider, true);
            }
            
            // 子オブジェクトのColliderもチェック
            Collider2D[] shooterChildColliders = shooter.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D childCollider in shooterChildColliders)
            {
                if (childCollider != arrowCollider)
                {
                    Physics2D.IgnoreCollision(arrowCollider, childCollider, true);
                }
            }
        }
    }

    void Update()
    {
        if (!isHit)
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isHit) return;

        if (collision.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>() != null)
        {
            return;
        }

        // 射手自身またはその子オブジェクトのColliderは除外
        if (shooter != null)
        {
            if (collision.transform == shooter.transform || 
                collision.transform.IsChildOf(shooter.transform) ||
                collision.GetComponentInParent<BowManNPC>() == shooter)
            {
                return;
            }
        }

        // PlayerまたはAllyタグのオブジェクトを対象にする
        if (collision.CompareTag("Player") || collision.CompareTag("Ally"))
        {
            StickToTarget(collision.transform, collision);
        }
        // Enemyタグのオブジェクトを対象にする
        else if (collision.CompareTag("Enemy"))
        {
            StickToTarget(collision.transform, collision);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isHit) return;

        if (collision.gameObject.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>() != null)
        {
            return;
        }

        // 射手自身またはその子オブジェクトのColliderは除外
        if (shooter != null)
        {
            if (collision.transform == shooter.transform || 
                collision.transform.IsChildOf(shooter.transform) ||
                collision.collider.GetComponentInParent<BowManNPC>() == shooter)
            {
                return;
            }
        }

        // PlayerまたはAllyタグのオブジェクトを対象にする
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Ally"))
        {
            StickToTarget(collision.transform, collision.collider);
        }
        // Enemyタグのオブジェクトを対象にする
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            StickToTarget(collision.transform, collision.collider);
        }
    }

    private void StickToTarget(Transform targetTransform, Collider2D hitCollider)
    {
        isHit = true;
        CancelInvoke(nameof(StickToGround));

        bool headshot = IsHeadshot(hitCollider);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        // 命中した瞬間の位置（実際にぶつかった場所）から、さらに矢の向きへ
        // penetrationDepth 分だけ進める。これにより、軸の中央付近が命中位置に来て、
        // 先端側はスプライトを貫通し、後端側は外に残る見た目になる。
        // （対象のpivot位置に単純にスナップすると、pivotが足元などにあるキャラクターの
        // 場合に実際の命中位置とズレてしまうため、この方式にしている)
        transform.position += (Vector3)(direction * penetrationDepth);

        transform.SetParent(targetTransform);

        // ArrowHitHandlerがある場合はそれを呼び出す（頭部命中なら即死、それ以外は通常の被弾処理）
        ArrowHitHandler hitHandler = targetTransform.GetComponent<ArrowHitHandler>();
        if (hitHandler != null)
        {
            if (headshot)
            {
                hitHandler.OnHeadshot();
            }
            else
            {
                hitHandler.OnHitByArrow();
            }

            // 射手が敵（EnemyHealthを持つ）で、この一撃でPlayer/Allyを倒した場合、
            // 射手自身の回避率を上昇させる
            CharacterHealth targetHealth = targetTransform.GetComponent<CharacterHealth>();
            if (targetHealth != null && targetHealth.isDead && shooter != null)
            {
                EnemyHealth shooterEnemyHealth = shooter.GetComponent<EnemyHealth>();
                shooterEnemyHealth?.OnEnemyDefeated();
            }
        }

        // EnemyHealthがある場合はダメージを与える（頭部命中なら盾を無視して即死）
        EnemyHealth enemyHealth = targetTransform.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            // 射手がPlayer/Ally側（CharacterHealthを持つ）なら、撃破者として渡す
            CharacterHealth shooterCharacterHealth = shooter != null ? shooter.GetComponent<CharacterHealth>() : null;

            if (headshot)
            {
                enemyHealth.TakeHeadshotDamage(shooterCharacterHealth);
            }
            else
            {
                // 矢の進行方向を攻撃者位置として渡す
                // shooterの位置を渡すことで、Swordsmanの盾ブロック判定が正しく動作する
                Vector3 attackerPosition = shooter != null ? shooter.transform.position : transform.position - (Vector3)(direction * penetrationDepth);
                enemyHealth.TakeDamage(1, attackerPosition, shooterCharacterHealth);
            }
        }

        // GameManager の仕組みを使って次のキャラクターに切り替える。
        // ただし、これは「今まさに操作しているキャラクター」に矢が当たった場合のみ行う。
        // 味方（Allyタグ）に当たっただけでは交代しない（retreat/被弾処理は上のOnHitByArrow()で別途行われる）。
        if (targetTransform.CompareTag("Player") && GameManager.Instance != null)
        {
            CharacterHealth charHealth = targetTransform.GetComponent<CharacterHealth>();
            if (charHealth != null)
            {
                GameManager.Instance.ReplacePlayer(charHealth);
            }
            else
            {
                GameManager.Instance.ReplacePlayer(targetTransform.gameObject);
            }

            Debug.Log("操作キャラクターに矢が突き刺さり、GameManager経由で次のキャラクターに切り替えました！");
        }
    }
}