using UnityEngine;

public class GhostTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 既存のプレイヤー専用スクリプト（CharacterHealth）を持つ場合
        CharacterHealth health = collision.GetComponent<CharacterHealth>();
        if (health != null && health.isPlayer && !health.isDead)
        {
            health.Die();
            return;
        }

        // 2. 後継キャラクター（NPCPlayerHelper 等）の場合：
        //    タグが "Player" であれば、スクリプトの種類に関わらず確実に死亡処理を呼び出す。
        //    （NPCPlayerHelper は CharacterHealth を持たないため、専用の Die() を呼ぶ）
        if (collision.CompareTag("Player"))
        {
            NPCPlayerHelper helper = collision.GetComponent<NPCPlayerHelper>();
            if (helper != null && !helper.isDead)
            {
                helper.Die();
                return;
            }
        }
    }
}