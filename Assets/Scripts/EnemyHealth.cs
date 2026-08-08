using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Settings")]
    public int maxHealth = 3;
    public int currentHealth;
    public bool isDead = false;

    [Header("Death Animation Settings")]
    [SerializeField] private float shakeDuration = 0.5f; // ぶるぶるする時間（秒）
    [SerializeField] private float shakeMagnitude = 0.1f; // ぶるぶるの強さ

    private SpriteRenderer spriteRenderer;
    private Vector3 originalLocalPosition;

    void Awake()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"敵（{gameObject.name}）がダメージを受けた！ 残り体力: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // 1. Disable collider
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 2. Animatorに専用の死亡演出（IsDeadトリガー、例：Hurtクリップ）があれば
        // それを再生する。ない場合は従来通りAnimatorを即座に無効化して
        // 汎用の赤化＋転倒演出（DeathAnimationRoutine）を使う。
        Animator anim = GetComponent<Animator>();
        bool hasDeathAnimation = anim != null
            && anim.runtimeAnimatorController != null
            && HasParameter(anim, "IsDead");

        if (hasDeathAnimation)
        {
            anim.SetTrigger("IsDead");
        }
        else if (anim != null)
        {
            anim.enabled = false;
        }

        // 3. Stop physics and velocity completely
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // 4. Automatically find and disable any movement scripts attached to this object
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != this && script.GetType() != typeof(GameManager))
            {
                script.enabled = false;
            }
        }

        // Start the death visual routine (死体としてその場に残る)
        StartCoroutine(DeathAnimationRoutine(hasDeathAnimation));
    }

    /// <summary>
    /// 指定した名前のパラメータがAnimatorに存在するかを確認する
    /// </summary>
    private bool HasParameter(Animator anim, string paramName)
    {
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }

    private System.Collections.IEnumerator DeathAnimationRoutine(bool hasDeathAnimation)
    {
        // 専用の死亡アニメーション（Hurtクリップ等）がある場合は、そちらの再生に任せる。
        // 回転・振動・赤化の汎用演出は行わない（Animatorも無効化しないため、
        // アニメーションはそのまま最終フレームで静止する）。
        if (hasDeathAnimation)
        {
            yield break;
        }

        // 先に元の位置を記憶
        originalLocalPosition = transform.localPosition;

        // 1. まずスプライトを真っ赤にする
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
        }

        // 2. 先に90度回転して横倒しにする
        transform.rotation = Quaternion.Euler(0, 0, 90f);

        float elapsed = 0.0f;

        // 3. 横倒しになった状態でぶるぶる痙攣させる
        while (elapsed < shakeDuration)
        {
            float offsetX = Random.Range(-1f, 1f) * shakeMagnitude;
            float offsetY = Random.Range(-1f, 1f) * shakeMagnitude;

            transform.localPosition = originalLocalPosition + (transform.right * offsetX) + (transform.up * offsetY);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 最終位置リセット（その場に死体として残る）
        transform.localPosition = originalLocalPosition;
    }
}