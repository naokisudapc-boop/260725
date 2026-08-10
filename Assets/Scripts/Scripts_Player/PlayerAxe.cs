using UnityEngine;
using System.Collections;

public class PlayerAxe : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 1440f; // 合成順序を修正したので、符号がそのまま画面上の回転方向に対応する

    private Collider2D axeCollider;
    private SpriteRenderer spriteRenderer; // ★追加：見た目だけを回すため
    private Quaternion originalRotation; // 最初に設定されている刃の向き（毎回このスイング前姿勢に戻す）
    private float currentZRotation = 0f;  // ★追加：回転角度の保持用
    private bool isSwinging = false;

    void Awake()
    {
        axeCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>(); // ★追加

        // プレハブで設定されている最初の刃の向きを記憶しておく
        originalRotation = transform.localRotation;

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
            // 画面（ワールド）のZ軸を軸に回転させる。originalRotationを基準に
            // ローカルZ軸で回す（post-multiply）と、originalRotationがX/Y軸を含む
            // 複雑な向きの場合にローカルZ軸が画面上でどちらを向くか予測できず、
            // rotationSpeedの符号だけでは狙った回転方向にならないことがある。
            // pre-multiplyにすることで、常にワールドのZ軸まわりの回転になる。
            currentZRotation += rotationSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.Euler(0, 0, currentZRotation) * originalRotation;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (axeCollider != null) axeCollider.enabled = false;

        // 最初に設定されていた刃の向きに正確に戻す（Quaternion.identityではない）
        transform.localRotation = originalRotation;
        isSwinging = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 敵への接触ダメージ処理
        EnemyHealth enemy = collision.gameObject.GetComponent<EnemyHealth>();
        if (enemy != null && !enemy.isDead)
        {
            Debug.Log($"💥【物理ヒット！】プレイヤーの斧が敵（{collision.name}）に直撃！");
            enemy.TakeDamage(1, transform.position);
        }
    }
}