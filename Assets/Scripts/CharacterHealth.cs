using UnityEngine;

public class CharacterHealth : MonoBehaviour
{
    [Header("Settings")]
    public string gender = "Male"; // "Male" or "Female"
    public bool isPlayer = false;
    public bool isControllable = true;
    public bool isDead = false;

    [Header("Death Animation Settings")]
    [SerializeField] private float shakeDuration = 0.5f; // ぶるぶるする時間（秒）
    [SerializeField] private float shakeMagnitude = 0.1f; // ぶるぶるの強さ

    private SpriteRenderer spriteRenderer;
    private Vector3 originalLocalPosition;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // Kキーが押されたときは死なずに、ただのキャラクター切り替え
        if (isPlayer && Input.GetKeyDown(KeyCode.K))
        {
            PassControlToNext();
        }
    }

    private void PassControlToNext()
    {
        isPlayer = false;
        isControllable = false;
        
        // ★修正：次のプレイヤーに主権を渡すので、自分のタグを通常に戻す
        gameObject.tag = "Untagged"; 

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReplacePlayer(this);
        }
    }

    // Die() が isPlayer を false にリセットしてしまう前に、死亡時点で
    // 自分が操作キャラクターだったかどうかを覚えておくためのフラグ。
    // DeathAnimationRoutine 側はこれを見て、操作キャラクター交代が
    // 必要かどうかを判断する。
    private bool wasPlayerOnDeath = false;

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        // 交代処理の要否判定用に、死亡時点で操作キャラクターだったかを保存
        wasPlayerOnDeath = isPlayer;

        isControllable = false;
        isPlayer = false;

        // 味方サイド（Player / Ally）が死亡した場合、その命を食料リソースとして
        // +1 還元する。タグを Untagged に変更する「前」に判定すること。
        bool wasAlly = gameObject.CompareTag("Player") || gameObject.CompareTag("Ally");
        if (wasAlly && GameManager.Instance != null)
        {
            GameManager.Instance.AddFood(1);
        }

        // ★修正：死亡が確定した時点で、自分はもうプレイヤーのタグを返上する
        gameObject.tag = "Untagged";

        // 1. Disable collider
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 2. Disable Animator to freeze animation instantly
        Animator anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

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

        // 5. Notify GameManager to recount the population (this character is now dead)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RecountPopulation();
        }

        // Start the death visual routine
        StartCoroutine(DeathAnimationRoutine());
    }

    private System.Collections.IEnumerator DeathAnimationRoutine()
    {
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

        // 最終位置リセット
        transform.localPosition = originalLocalPosition;

        // 4. 操作キャラクターが死亡した場合のみ、次のキャラクターへ切り替える。
        // 味方NPC（未操作）が死んだだけでは操作キャラクターは変更しない。
        if (wasPlayerOnDeath && GameManager.Instance != null)
        {
            GameManager.Instance.ReplacePlayer(this);
        }
    }
}