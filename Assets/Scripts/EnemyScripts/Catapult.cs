using UnityEngine;
using System.Collections;

/// <summary>
/// 岩石を放物線状に投擲するユニット。BowManNPCと違い、射線上に障害物があっても
/// 遠くの敵をピンポイントで攻撃できる（着弾位置は対象の座標そのものを使うため）。
/// スプライト・アニメーションは右斜め手前を向いた状態が基準になっているため、
/// 移動方向・発射方向に応じて transform を回転させて向きを合わせる。
/// 死亡演出（Hurtクリップの再生）は EnemyHealth 側で IsDead トリガーが自動的に処理する。
/// </summary>
public class Catapult : MonoBehaviour
{
    [Header("Combat Settings")]
    [Tooltip("索敵範囲")]
    public float detectRange = 10.0f;
    [Tooltip("これ以上遠いと発射できない上限（0以下で無制限）")]
    public float maxAttackRange = 0f;
    [Tooltip("構えてから着弾までの合計時間の目安（距離に応じて多少前後する）")]
    public float chargeTime = 0.6f;
    [Tooltip("投擲後のクールタイム")]
    public float attackCooldown = 2.0f;

    [Header("Rock Projectile Settings")]
    [Tooltip("投げる岩のスプライト（Assets/Images内の岩石スプライトを割り当てる）")]
    [SerializeField] private Sprite rockSprite;
    [Tooltip("岩が飛んでいる時間（秒）")]
    [SerializeField] private float rockFlightDuration = 0.8f;
    [Tooltip("放物線の高さ（大きいほど高く弧を描く）")]
    [SerializeField] private float arcHeight = 2.5f;
    [Tooltip("岩のスプライトの大きさ")]
    [SerializeField] private float rockScale = 0.3f;
    [Tooltip("岩スプライトの描画Sorting Layer名（未設定/存在しない場合はDefaultのまま）")]
    [SerializeField] private string rockSortingLayerName = "Enemy";
    [Tooltip("岩スプライトの描画順（同じSorting Layer内での前後関係）")]
    [SerializeField] private int rockSortingOrder = 50;

    private Transform target;
    private Animator animator;
    private bool isAttacking = false;
    private Vector2 facingDirection = Vector2.right; // 基準の向き（右）

    // 操作キャラクターになったとき用
    [Header("Manual Aiming (操作キャラクターになったとき用)")]
    [SerializeField] private float aimLineLength = 8f;
    [SerializeField] private LineRenderer aimLineRenderer;
    private CharacterHealth characterHealth;
    private bool isAiming = false;
    private Vector2 aimDirection = Vector2.right;
    private Vector3 aimTargetPoint;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        characterHealth = GetComponent<CharacterHealth>();

        EnsureAimLineRenderer();

        if (rockSprite == null)
        {
            Debug.LogWarning("[Catapult] Rock Sprite が未設定です。Inspectorで岩石のスプライトを割り当ててください。");
        }
    }

    void Update()
    {
        if (characterHealth != null && characterHealth.isPlayer)
        {
            UpdateAsControlledPlayer();
            return;
        }

        UpdateAsAI();
    }

    // ============================================================
    //  自律AI（敵として動くとき）
    // ============================================================
    private void UpdateAsAI()
    {
        FindTarget();

        if (target == null || isAttacking)
        {
            return;
        }

        float distance = Vector2.Distance(transform.position, target.position);
        if (maxAttackRange > 0f && distance > maxAttackRange)
        {
            return;
        }

        // ターゲットの方を向く（障害物を無視してそのまま照準できる＝ピンポイント攻撃の特徴）
        Vector2 dirToTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;
        FaceDirection(dirToTarget);

        StartCoroutine(AttackRoutine(target.position));
    }

    /// <summary>
    /// PlayerまたはAllyタグのオブジェクトを索敵する（女性NPCは非戦闘員として除外）
    /// </summary>
    private void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject[] allies = GameObject.FindGameObjectsWithTag("Ally");

        GameObject closestTarget = null;
        float closestDistance = detectRange;

        if (player != null)
        {
            float d = Vector2.Distance(transform.position, player.transform.position);
            if (d <= closestDistance)
            {
                CharacterHealth health = player.GetComponentInParent<CharacterHealth>();
                if (health == null || !health.isDead)
                {
                    closestTarget = player;
                    closestDistance = d;
                }
            }
        }

        foreach (GameObject ally in allies)
        {
            if (ally == gameObject) continue;

            FarmingNPC farmingData = ally.GetComponent<FarmingNPC>();
            if (farmingData != null && farmingData.gender == Gender.Female) continue;
            NPCPlayerHelper helperData = ally.GetComponent<NPCPlayerHelper>();
            if (helperData != null && helperData.gender == Gender.Female) continue;

            float d = Vector2.Distance(transform.position, ally.transform.position);
            if (d <= closestDistance)
            {
                CharacterHealth health = ally.GetComponentInParent<CharacterHealth>();
                if (health != null && health.isDead) continue;

                closestTarget = ally;
                closestDistance = d;
            }
        }

        target = closestTarget != null ? closestTarget.transform : null;
    }

    /// <summary>
    /// 構え→投擲→着弾のルーチン。着弾位置は指定座標そのもの（障害物を無視）。
    /// </summary>
    private IEnumerator AttackRoutine(Vector3 targetPosition)
    {
        isAttacking = true;

        if (animator != null) animator.SetTrigger("Shooting_Before");
        yield return new WaitForSeconds(chargeTime);

        if (animator != null) animator.SetTrigger("Shooting_After");
        LaunchRock(targetPosition);

        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

    /// <summary>
    /// 岩を放物線状に飛ばす。targetPositionへピンポイントで着弾する。
    /// </summary>
    private void LaunchRock(Vector3 targetPosition)
    {
        GameObject rockObj = new GameObject("CatapultRock");
        rockObj.transform.position = transform.position;
        rockObj.transform.localScale = Vector3.one * rockScale;

        SpriteRenderer sr = rockObj.AddComponent<SpriteRenderer>();
        sr.sprite = rockSprite;

        // Sorting Layerを明示的に指定する（未指定だとDefaultレイヤーになり、
        // 地面タイルマップ等の下に隠れて見えなくなることがあるため）
        if (!string.IsNullOrEmpty(rockSortingLayerName) && SortingLayerExists(rockSortingLayerName))
        {
            sr.sortingLayerName = rockSortingLayerName;
        }
        sr.sortingOrder = rockSortingOrder;

        CatapultRock rock = rockObj.AddComponent<CatapultRock>();
        rock.Init(transform.position, targetPosition, rockFlightDuration, arcHeight, gameObject);
    }

    private bool SortingLayerExists(string layerName)
    {
        foreach (var layer in SortingLayer.layers)
        {
            if (layer.name == layerName) return true;
        }
        return false;
    }

    /// <summary>
    /// スプライトの基準向き（右）から、指定方向を向くよう回転させる
    /// </summary>
    private void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        facingDirection = direction;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (animator != null)
        {
            animator.SetFloat("LastInputX", direction.x);
            animator.SetFloat("LastInputY", direction.y);
        }
    }

    // ============================================================
    //  操作キャラクターになったときの手動照準
    // ============================================================
    private void EnsureAimLineRenderer()
    {
        if (aimLineRenderer != null) return;

        GameObject lineObj = new GameObject("AimLine");
        lineObj.transform.SetParent(transform, false);
        aimLineRenderer = lineObj.AddComponent<LineRenderer>();
        aimLineRenderer.positionCount = 2;
        aimLineRenderer.startWidth = 0.05f;
        aimLineRenderer.endWidth = 0.05f;
        aimLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        aimLineRenderer.startColor = new Color(1f, 0.6f, 0.2f, 0.8f);
        aimLineRenderer.endColor = new Color(1f, 0.6f, 0.2f, 0.2f);
        aimLineRenderer.sortingOrder = 100;
        aimLineRenderer.enabled = false;
    }

    private void UpdateAsControlledPlayer()
    {
        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            isAiming = true;
            if (aimLineRenderer != null) aimLineRenderer.enabled = true;
        }

        if (isAiming && Input.GetMouseButton(0))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = transform.position.z;

            Vector2 dragVector = (Vector2)mouseWorldPos - (Vector2)transform.position;
            if (dragVector.sqrMagnitude > 0.0001f)
            {
                // 弓と同じく、ドラッグした方向とは逆方向へ投擲する
                aimDirection = -dragVector.normalized;
            }
            aimTargetPoint = (Vector3)((Vector2)transform.position + aimDirection * aimLineLength);

            FaceDirection(aimDirection);

            if (aimLineRenderer != null)
            {
                aimLineRenderer.SetPosition(0, transform.position);
                aimLineRenderer.SetPosition(1, aimTargetPoint);
            }
        }

        if (isAiming && Input.GetMouseButtonUp(0))
        {
            isAiming = false;
            if (aimLineRenderer != null) aimLineRenderer.enabled = false;

            if (!isAttacking)
            {
                StartCoroutine(AttackRoutine(aimTargetPoint));
            }
        }
    }
}
