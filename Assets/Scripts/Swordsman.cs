using UnityEngine;
using System.Collections;

/// <summary>
/// 剣と盾を持つ敵NPC。近接で接近して斬りつける。
/// 盾を構えている方向（正面）からの攻撃は、一定確率（デフォルト90%）で無効化する。
/// アニメーションは Assets/Animations/Swordsman の Animation.controller を使用し、
/// BowManNPC等と同じパラメータ構成（InputX/InputY/Speed/LastInputX/LastInputY/
/// Shooting_Before/Shooting_After トリガー）で動作する
/// （トリガー名は歴史的経緯で "Shooting_*" のままだが、実際には剣を構える/斬る動作を表す）。
/// 死亡時は EnemyHealth 側で IsDead トリガーが発火し、Hurt クリップが再生される。
/// </summary>
public class Swordsman : MonoBehaviour
{
    [Header("Combat Settings")]
    [Tooltip("索敵範囲")]
    public float detectRange = 6.0f;
    [Tooltip("攻撃射程（この距離まで近づいたら斬りつける）")]
    public float attackRange = 1.2f;
    [Tooltip("移動速度")]
    public float moveSpeed = 2.0f;
    [Tooltip("剣を構えてから実際に斬るまでの時間（Shooting_Beforeの再生時間に合わせる）")]
    public float chargeTime = 0.4f;
    [Tooltip("攻撃後の硬直時間")]
    public float attackCooldown = 1.0f;

    [Header("Shield Settings")]
    [Tooltip("正面からの攻撃を無効化する確率（0〜1）")]
    [Range(0f, 1f)]
    [SerializeField] private float shieldBlockChance = 0.9f;
    [Tooltip("この値以上の内積なら「正面」とみなす（1に近いほど正面判定が狭くなる）")]
    [Range(-1f, 1f)]
    [SerializeField] private float frontDotThreshold = 0.5f;

    private Transform target;
    private Animator animator;
    private Rigidbody2D rb;
    private bool isAttacking = false;
    [Header("Shield Block Reaction")]
    [Tooltip("盾で攻撃を防いだときに後退する速度")]
    [SerializeField] private float shieldKnockbackSpeed = 3.5f;
    [Tooltip("盾ブロック時のノックバック時間")]
    [SerializeField] private float shieldKnockbackDuration = 0.12f;
    private Coroutine shieldKnockbackRoutine;


    // 直近の移動・攻撃方向。盾の正面判定に使う（LastInputX/LastInputYと同じ役割）
    private Vector2 facingDirection = Vector2.down;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        if (rb != null) rb.freezeRotation = true;
    }

    void Update()
    {
        FindTarget();

        if (target == null)
        {
            if (!isAttacking)
            {
                Move(Vector2.zero, 0f);
                UpdateAnimation(Vector2.zero);
            }
            return;
        }

        if (isAttacking) return;

        float distance = Vector2.Distance(transform.position, target.position);

        if (distance > attackRange)
        {
            // 追いかける
            Vector2 moveDirection = (target.position - transform.position).normalized;
            Move(moveDirection, moveSpeed);
            UpdateAnimation(moveDirection);
        }
        else
        {
            // 射程内：立ち止まって斬りつける
            Move(Vector2.zero, 0f);
            UpdateAnimation(Vector2.zero);
            StartCoroutine(AttackRoutine());
        }
    }

    /// <summary>
    /// PlayerまたはAllyタグのオブジェクトを探す（女性NPCは非戦闘員として除外）
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

            // 女性NPC（農作業などの非戦闘員）は攻撃対象から除外する
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
    /// 剣を構えてから斬りつける一連の動作。
    /// Shooting_Before（構え）→ chargeTime待機 → Shooting_After（斬撃）→ ダメージ判定
    /// </summary>
    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetTrigger("Shooting_Before");
        }

        yield return new WaitForSeconds(chargeTime);

        if (animator != null)
        {
            animator.SetTrigger("Shooting_After");
        }

        // 斬撃の瞬間、まだ射程内にターゲットがいればダメージを与える
        if (target != null)
        {
            float distance = Vector2.Distance(transform.position, target.position);
            if (distance <= attackRange + 0.3f)
            {
                CharacterHealth health = target.GetComponentInParent<CharacterHealth>();
                if (health != null && !health.isDead)
                {
                    health.Die();
                }
            }
        }

        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

        /// <summary>
    /// 盾で攻撃を無効化したとき、攻撃者から離れる方向へ短く後退する。
    /// </summary>
    public void OnShieldBlock(Vector3 attackerPosition)
    {
        Vector2 knockbackDirection = ((Vector2)transform.position - (Vector2)attackerPosition).normalized;
        if (knockbackDirection.sqrMagnitude < 0.0001f) return;

        if (shieldKnockbackRoutine != null)
        {
            StopCoroutine(shieldKnockbackRoutine);
        }
        shieldKnockbackRoutine = StartCoroutine(ShieldKnockbackRoutine(knockbackDirection));
    }

    private IEnumerator ShieldKnockbackRoutine(Vector2 direction)
    {
        if (rb != null)
        {
            rb.linearVelocity = direction * shieldKnockbackSpeed;
        }

        yield return new WaitForSeconds(shieldKnockbackDuration);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        shieldKnockbackRoutine = null;
    }

private void Move(Vector2 direction, float speed)
    {
        if (rb != null) rb.linearVelocity = direction * speed;
    }

    private void UpdateAnimation(Vector2 moveInput)
    {
        if (animator == null) return;

        if (moveInput.magnitude > 0.1f)
        {
            facingDirection = moveInput.normalized;
            animator.SetFloat("InputX", moveInput.x);
            animator.SetFloat("InputY", moveInput.y);
            animator.SetFloat("Speed", moveInput.magnitude);
            animator.SetFloat("LastInputX", moveInput.x);
            animator.SetFloat("LastInputY", moveInput.y);
        }
        else
        {
            animator.SetFloat("InputX", 0);
            animator.SetFloat("InputY", 0);
            animator.SetFloat("Speed", 0);
        }
    }

    /// <summary>
    /// 盾によるブロック判定。EnemyHealth.TakeDamage()から呼ばれる。
    /// 攻撃者がこちらの正面（facingDirection）にいる場合、shieldBlockChanceの確率で
    /// 攻撃を無効化する。背後・側面からの攻撃はブロックしない。
    /// </summary>
    public bool ShouldBlockAttack(Vector3 attackerPosition)
    {
        Vector2 toAttacker = (Vector2)attackerPosition - (Vector2)transform.position;
        if (toAttacker.sqrMagnitude < 0.0001f) return false;
        toAttacker.Normalize();

        float dot = Vector2.Dot(facingDirection, toAttacker);
        if (dot >= frontDotThreshold)
        {
            return Random.value < shieldBlockChance;
        }

        return false;
    }
}
