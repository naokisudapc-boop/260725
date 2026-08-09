using UnityEngine;

public class Arrow : MonoBehaviour
{
    private float speed = 15f;
    private Vector2 direction;
    private bool isHit = false;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
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