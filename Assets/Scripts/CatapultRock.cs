using UnityEngine;
using System.Collections;

/// <summary>
/// カタパルトが投げる岩石。放物線を描いて指定座標へピンポイントで着弾する。
/// 障害物を無視して飛ぶため、飛行中は何にも衝突判定を行わない。
/// </summary>
public class CatapultRock : MonoBehaviour
{
    private Vector3 startPos;
    private Vector3 endPos;
    private float duration;
    private float arcHeight;
    private GameObject shooter;

    public void Init(Vector3 start, Vector3 end, float flightDuration, float height, GameObject shooterObj)
    {
        startPos = start;
        endPos = end;
        duration = Mathf.Max(0.05f, flightDuration);
        arcHeight = height;
        shooter = shooterObj;

        StartCoroutine(FlyAndLand());
    }

    private IEnumerator FlyAndLand()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector3 flatPos = Vector3.Lerp(startPos, endPos, t);
            // sinカーブで放物線状の高さを加える
            float height = Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.position = flatPos + new Vector3(0, height, 0);

            // 岩自体も少し回転させて飛んでいる感を出す
            transform.Rotate(0, 0, 360f * Time.deltaTime / duration);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        Impact();
    }

    /// <summary>
    /// 着弾時のダメージ処理。着弾地点そのものを直接判定するため、
    /// 途中の障害物は一切影響しない（ピンポイント攻撃）。
    /// </summary>
    private void Impact()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.5f);
        bool hitSomething = false;

        foreach (Collider2D hit in hits)
        {
            if (shooter != null && (hit.transform == shooter.transform || hit.transform.IsChildOf(shooter.transform)))
            {
                continue;
            }

            if (hit.CompareTag("Player") || hit.CompareTag("Ally"))
            {
                CharacterHealth health = hit.GetComponentInParent<CharacterHealth>();
                if (health != null && !health.isDead)
                {
                    health.Die();
                    hitSomething = true;
                }
            }
            else if (hit.CompareTag("Enemy"))
            {
                EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null && !enemyHealth.isDead)
                {
                    CharacterHealth shooterHealth = shooter != null ? shooter.GetComponent<CharacterHealth>() : null;
                    enemyHealth.TakeDamage(1, transform.position, shooterHealth);
                    hitSomething = true;
                }
            }
        }

        Destroy(gameObject, hitSomething ? 0.1f : 0.3f);
    }
}
