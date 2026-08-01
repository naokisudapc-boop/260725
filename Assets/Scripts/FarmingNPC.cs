using UnityEngine; 
using UnityEngine.Tilemaps;
using System.Collections;

public class FarmingNPC : MonoBehaviour
{
    public Gender gender; 
    public float moveSpeed = 3f;

    [Header("Spawn Rate Attribute (Female Only)")]
    public float femaleSpawnRateAttribute = 0f;

    [HideInInspector] public string characterName; 

    /// <summary>
    /// 女性NPCのスポーンレート（1〜99%）を外部から設定するセッター。
    /// 誕生時に NPCSpawner 等からランダム値を代入するために使用する。
    /// </summary>
    public void SetSpawnRate(int rate)
    {
        femaleSpawnRateAttribute = Mathf.Clamp(rate, 1, 99);
    }
    
    protected Tilemap targetTilemap;
    protected Vector3Int targetGridPos;
    protected FarmTileData farmTileBase;
    private bool isWorking = false;
    protected Animator animator;
    protected Rigidbody2D rb;
    private bool isFinished = false;
    private Vector3 finishedPosition;
    // 一度水やりを開始したら、その土地に完全固定し、絶対にターゲットを
    // 切り替えて別の土地へ移動（放棄）しないようにするロックフラグ。
    private bool isLockedToFarm = false;

    // 矢の被弾処理用コンポーネント
    protected ArrowHitHandler arrowHitHandler;

    public bool IsBusy => isWorking || isFinished;
    // 外部（PlayerFarming など）からこの NPC に新しい水やりを割り当てられるか
    public bool CanAcceptNewTask => !isWorking && !isFinished && !isLockedToFarm;

    protected virtual void Awake()
    {
        // Ensure this NPC is counted as an ally by the population system.
        // Set the tag as early as possible (before GameManager's delayed count)
        // so the initial Population UI reflects all pre-placed ally NPCs.
        // ThiefNPC overrides Awake and also sets this, but we cover the base
        // case (e.g. female NPCs) here as well.
        if (!gameObject.CompareTag("Player"))
        {
            gameObject.tag = "Ally";
        }
        
        // ArrowHitHandlerの取得または追加
        arrowHitHandler = GetComponent<ArrowHitHandler>();
        if (arrowHitHandler == null)
        {
            arrowHitHandler = gameObject.AddComponent<ArrowHitHandler>();
        }
        
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.linearDamping = 10f;
        rb.freezeRotation = true;
    }

    protected virtual void Start()
    {
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            if (HasParameter(animator, "Gender")) animator.SetInteger("Gender", (int)gender);
        }

        characterName = NameGenerator.GetRandomName(gender);
        Debug.Log($"NPCの名前が決定しました: {characterName}");

        if (UIManager.Instance != null)
        {
            Vector3 nameOffset = new Vector3(0f, 1.0f, -0.1f);
            UIManager.Instance.CreateNameLabel(transform, characterName, nameOffset);
        }
    }

    void Update()
    {
        // 土地に固定されている（ロック中）場合は、絶対に水やりを放棄して
        // 別の土地へ移動することがないよう、CancelWateredStatus を呼ばない。
        if (isFinished && !isLockedToFarm)
        {
            if (Vector3.Distance(transform.position, finishedPosition) > 0.01f)
            {
                CancelWateredStatus();
            }
        }
    }

    public void AssignWateringTask(Tilemap tilemap, Vector3Int gridPos, FarmTileData tileBase)
    {
        if (tilemap == null || tileBase == null) return; 
        if (isWorking || isFinished || isLockedToFarm) return;

        targetTilemap = tilemap;
        targetGridPos = gridPos;
        farmTileBase = tileBase;

        StartCoroutine(DoWateringWork());
    }

    private IEnumerator DoWateringWork()
    {
        isWorking = true;
        // 水やりを開始した瞬間に、この土地へ完全固定（ロック）する。
        // 以降は周囲に新しい耕作地が出現してもターゲットを切り替えない。
        isLockedToFarm = true;

        Vector3 targetWorldPos = targetTilemap.GetCellCenterWorld(targetGridPos);

        while (Vector3.Distance(transform.position, targetWorldPos) > 0.05f)
        {
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
            Vector3 moveDirection = (nextPosition - transform.position).normalized;

            if (animator != null)
            {
                // 存在するパラメータにのみ安全に値を更新する（警告連打を防ぐ）
                UpdateAnimatorFloat("MoveX", moveDirection.x);
                UpdateAnimatorFloat("MoveY", moveDirection.y);
                UpdateAnimatorFloat("InputX", moveDirection.x);
                UpdateAnimatorFloat("InputY", moveDirection.y);
                UpdateAnimatorFloat("Speed", moveDirection.magnitude);
            }

            transform.position = nextPosition;
            yield return null;
        }

        if (animator != null)
        {
            UpdateAnimatorFloat("MoveX", 0f);
            UpdateAnimatorFloat("MoveY", 0f);
            UpdateAnimatorFloat("InputX", 0f);
            UpdateAnimatorFloat("InputY", 0f);
            UpdateAnimatorFloat("Speed", 0f);
        }

        yield return new WaitForSeconds(0.5f);

        FarmTileData newTile = ScriptableObject.CreateInstance<FarmTileData>();
        newTile.plowedSprite = farmTileBase.plowedSprite;
        newTile.wateredSprite = farmTileBase.wateredSprite;
        newTile.status = FarmTileData.TileStatus.Watered;

        targetTilemap.SetTile(targetGridPos, newTile);
        if (GameManager.Instance != null) GameManager.Instance.UpdateFoodByWateredFarms();

        isWorking = false;
        isFinished = true;
        finishedPosition = transform.position;

        // 以降はこの位置から一歩も動かない。ロックは解除しない。
    }

    private void CancelWateredStatus()
    {
        // ロック中は絶対に呼ばれてはならない（Update 側でガード済み）。
        if (isLockedToFarm) return;
        if (targetTilemap == null) return;

        FarmTileData originalTile = ScriptableObject.CreateInstance<FarmTileData>();
        originalTile.plowedSprite = farmTileBase.plowedSprite;
        originalTile.wateredSprite = farmTileBase.wateredSprite;
        originalTile.status = FarmTileData.TileStatus.Plowed;

        targetTilemap.SetTile(targetGridPos, originalTile);
        if (GameManager.Instance != null) GameManager.Instance.UpdateFoodByWateredFarms();

        isFinished = false;
    }

    protected void UpdateAnimatorFloat(string paramName, float value)
    {
        if (animator != null && HasParameter(animator, paramName))
        {
            animator.SetFloat(paramName, value);
        }
    }

    protected bool HasParameter(Animator anim, string paramName)
    {
        if (anim == null) return false;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }
}