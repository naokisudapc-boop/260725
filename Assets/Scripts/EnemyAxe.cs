using UnityEngine;
using System.Collections;

public class EnemyAxe : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 720f; // 1秒間に回転する角度

    private Collider2D axeCollider;
    private Quaternion originalRotation;
    private bool isSwinging = false;

    void Awake()
    {
        axeCollider = GetComponent<Collider2D>();
        if (axeCollider != null)
        {
            axeCollider.enabled = false; // 普段はオフ
        }
        else
        {
            Debug.LogError($"【要確認】{gameObject.name} に Collider 2D が付いていません！攻撃が当たらない原因になります。", gameObject);
        }
        
        originalRotation = transform.localRotation;
    }

    public void ExecuteAttack()
    {
        if (isSwinging) return;
        StartCoroutine(SwingAndSpinAxe());
    }

    private IEnumerator SwingAndSpinAxe()
    {
        isSwinging = true;
        
        if (axeCollider != null) axeCollider.enabled = true;

        float duration = 0.3f;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (axeCollider != null) axeCollider.enabled = false;
        transform.localRotation = originalRotation;
        
        isSwinging = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // ヒットしたオブジェクト、またはその親から CharacterHealth を取得する
        CharacterHealth health = collision.GetComponentInParent<CharacterHealth>();
        if (health == null)
        {
            health = collision.GetComponent<CharacterHealth>();
        }

        // Player タグ、Ally タグ（非操作中の味方NPC）、または FarmingNPC / ThiefNPC /
        // NPCPlayerHelper を持つ味方も対象にする
        bool isTarget = collision.CompareTag("Player")
            || collision.CompareTag("Ally")
            || collision.gameObject.GetComponent<FarmingNPC>() != null
            || collision.gameObject.GetComponent<ThiefNPC>() != null
            || collision.gameObject.GetComponent<NPCPlayerHelper>() != null;

        if (health != null && !health.isDead && isTarget)
        {
            Debug.Log($"💥【痛い！】敵の斧が {collision.gameObject.name} にヒットしました！");
            health.Die(); // ★確実に体力を減らして死亡させる

            // 一度ヒットしたらコライダーを無効化し、連続ヒットを防ぐ
            if (axeCollider != null) axeCollider.enabled = false;
        }
    }
}