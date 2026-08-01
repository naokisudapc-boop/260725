using UnityEngine;

public class TreeController : MonoBehaviour
{
    [SerializeField] private int maxHp = 1;
    private int currentHp;

    // 切り株の画像がある場合、インスペクターから設定
    [SerializeField] private Sprite stumpSprite; 
    private SpriteRenderer spriteRenderer;
    private Collider2D treeCollider;
    private bool isCutDown = false;

    void Start()
    {
        currentHp = maxHp;
        spriteRenderer = GetComponent<SpriteRenderer>();
        treeCollider = GetComponent<Collider2D>();
    }

    // ダメージを受ける関数
    public void TakeDamage(int damage)
    {
        if (isCutDown) return;

        currentHp -= damage;
        Debug.Log($"木に{damage}のダメージ！ 残りHP: {currentHp}");

        // 軽微な揺れエフェクトなどをここに足すと気持ちよくなります

        if (currentHp <= 0)
        {
            CutDown();
        }
    }

    // 伐採された時の処理
    private void CutDown()
    {
        isCutDown = true;
        Debug.Log("木が切り倒された！");

        // ★ここに追記：GameManagerに木が切られたことを伝える
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddTreeCount();
        }

        if (stumpSprite != null)
        {
            spriteRenderer.sprite = stumpSprite;
            treeCollider.enabled = false; 
        }
        else
        {
            Destroy(gameObject);
        }

        // アイテム（木材）をドロップさせる場合はここに書く
        // Instantiate(woodItemPrefab, transform.position, Quaternion.identity);

        if (stumpSprite != null)
        {
            // 切り株の画像に変更し、衝突判定を消す（または小さくする）
            spriteRenderer.sprite = stumpSprite;
            treeCollider.enabled = false; 
        }
        else
        {
            // 切り株がない場合はそのまま消滅
            Destroy(gameObject);
        }
    }
}