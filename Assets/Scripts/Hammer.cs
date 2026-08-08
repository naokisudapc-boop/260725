using UnityEngine;
using System.Collections;

public class Hammer : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 1440f; // 1秒間に回転する角度（PlayerAxe.csと同じ値に統一）

    [Header("Sprite Settings")]
    [SerializeField] public Sprite turuhashiSprite; // ツルハシスプライト

    private Collider2D hammerCollider;
    private Quaternion originalRotation;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        hammerCollider = GetComponent<Collider2D>();
        if (hammerCollider != null)
        {
            hammerCollider.enabled = false; // 普段はオフ
        }
        else
        {
            Debug.LogError($"【要確認】{gameObject.name} に Collider 2D（コライダー）が付いていません！おばけが消せない原因になります。", gameObject);
        }

        originalRotation = transform.localRotation;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void ChangeToTuruhashi()
    {
        if (turuhashiSprite != null)
        {
            spriteRenderer.sprite = turuhashiSprite;
        }
    }

    public void ExecuteAttack()
    {
        StartCoroutine(SwingAndSpinHammer());
    }

    public IEnumerator SwingAndSpinHammer()
    {
        Debug.Log($"{transform.parent.name} のハンマー攻撃（回転演出）を開始します。");

        if (hammerCollider != null) hammerCollider.enabled = true;

        float duration = 0.3f; // 攻撃持続時間
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null; // 1フレーム待つ
        }

        if (hammerCollider != null) hammerCollider.enabled = false;
        transform.localRotation = originalRotation;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Ghost"))
        {
            Debug.Log($"ハンマーがおばけ（{collision.gameObject.name}）をヒット！消去します。");
            Destroy(collision.gameObject);
        }
        else if (collision.CompareTag("Enemy"))
        {
            EnemyHealth enemyHealth = collision.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null)
            {
                enemyHealth = collision.GetComponent<EnemyHealth>();
            }

            if (enemyHealth != null && !enemyHealth.isDead)
            {
                Debug.Log($"🔨 ハンマーが敵（{collision.gameObject.name}）をヒット！ダメージを与えます。");
                enemyHealth.TakeDamage(1);
            }
        }
    }
}