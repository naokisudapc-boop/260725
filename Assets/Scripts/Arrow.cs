using UnityEngine;

public class Arrow : MonoBehaviour
{
    private float speed = 15f;
    private Vector2 direction;
    private bool isHit = false;
    

    private Rigidbody2D rb;

    [Header("Lifetime Settings")]
    [Tooltip("矢の滞空時間（秒）。命中したかどうかに関わらず、この時間が経過すると自動的に消える")]
    [SerializeField] private float lifeTime = 5f;

    [Header("Penetration Settings")]
    [Tooltip("命中した瞬間の位置から、矢の向きにさらに進める距離。値を大きくするほど深く突き刺さり、先端がスプライトを貫通して見える")]
    [SerializeField] private float penetrationDepth = 0.3f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (lifeTime > 0f)
        {
            Destroy(gameObject, lifeTime);
        }
    }

        

public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
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

        // PlayerまたはAllyタグのオブジェクトを対象にする
        if (collision.CompareTag("Player") || collision.CompareTag("Ally"))
        {
            StickToTarget(collision.transform);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isHit) return;

        if (collision.gameObject.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>() != null)
        {
            return;
        }

        // PlayerまたはAllyタグのオブジェクトを対象にする
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Ally"))
        {
            StickToTarget(collision.transform);
        }
    }

    private void StickToTarget(Transform targetTransform)
    {
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

        // 命中した瞬間の位置（実際にぶつかった場所）から、さらに矢の向きへ
        // penetrationDepth 分だけ進める。これにより、軸の中央付近が命中位置に来て、
        // 先端側はスプライトを貫通し、後端側は外に残る見た目になる。
        // （対象のpivot位置に単純にスナップすると、pivotが足元などにあるキャラクターの
        // 場合に実際の命中位置とズレてしまうため、この方式にしている）
        transform.position += (Vector3)(direction * penetrationDepth);

        transform.SetParent(targetTransform);

        // ArrowHitHandlerがある場合はそれを呼び出す
        ArrowHitHandler hitHandler = targetTransform.GetComponent<ArrowHitHandler>();
        if (hitHandler != null)
        {
            hitHandler.OnHitByArrow();
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