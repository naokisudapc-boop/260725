using UnityEngine;
using System.Collections;

public class PlayerAxe : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 1440f;

    private Collider2D axeCollider;
    private SpriteRenderer spriteRenderer; // ★追加：見た目だけを回すため
    private float currentZRotation = 0f;  // ★追加：回転角度の保持用
    private bool isSwinging = false;

    void Awake()
    {
        axeCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>(); // ★追加
        
        if (axeCollider != null) axeCollider.enabled = false; // 普段はオフ
    }

    public void ExecuteAttack()
    {
        if (isSwinging) return;
        StartCoroutine(SwingAndSpinAxe());
    }

    private IEnumerator SwingAndSpinAxe()
    {
        isSwinging = true;
        if (axeCollider != null) axeCollider.enabled = true; // 攻撃中のみON

        float duration = 0.3f;
        float elapsed = 0.0f;
        currentZRotation = 0f; // 回転をリセット

        while (elapsed < duration)
        {
            // ★修正：transform 自体ではなく、スプライトの角度（またはtransformの回転のみ）を回す
            // 軸がズレないように transform.localRotation のみを安全に回転させます
            currentZRotation += rotationSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.Euler(0, 0, currentZRotation);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (axeCollider != null) axeCollider.enabled = false;
        
        // 元の角度（0度）に綺麗に戻す
        transform.localRotation = Quaternion.identity;
        isSwinging = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 敵への接触ダメージ処理
        EnemyHealth enemy = collision.gameObject.GetComponent<EnemyHealth>();
        if (enemy != null && !enemy.isDead)
        {
            Debug.Log($"💥【物理ヒット！】プレイヤーの斧が敵（{collision.name}）に直撃！");
            enemy.TakeDamage(1);
        }
    }
}