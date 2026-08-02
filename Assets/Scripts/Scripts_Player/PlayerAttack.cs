using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.8f;
    [SerializeField] private LayerMask treeLayer;
    [SerializeField] private int attackDamage = 1;

    [Header("Iron Mining Settings (Player)")]
    [SerializeField] private Tilemap ironTilemap;
    [SerializeField] private TileBase afterMinedTile;
    [SerializeField] private GameObject ironOrePrefab;

    [Header("Visual Juice")]
    [SerializeField] private float jumpDuration = 0.3f;

    private SpriteRenderer axeSpriteRenderer;
    private PlayerAxe playerAxe;
    private Hammer hammer;
    private bool isAttacking = false;

    void Start()
    {
        ResolveAttackPoint();
        AutoSetupMiningSettings(); // 動的追加時に自動で設定を補完する
    }

    /// <summary>
    /// シーン内の MiningTilemap や ThiefNPC から自動的に採掘用設定を取得する
    /// </summary>
    public void AutoSetupMiningSettings()
    {
        // 1. MiningTilemap が未設定の場合、ヒエラルキーから名前で自動検索
        if (ironTilemap == null)
        {
            GameObject miningObj = GameObject.Find("MiningTilemap");
            if (miningObj != null)
            {
                ironTilemap = miningObj.GetComponent<Tilemap>();
            }
        }

        // 2. プレハブやタイルが未設定の場合、シーン内の ThiefNPC から設定をコピー
        if (afterMinedTile == null || ironOrePrefab == null)
        {
            ThiefNPC thief = Object.FindAnyObjectByType<ThiefNPC>();
            if (thief != null)
            {
                if (afterMinedTile == null) afterMinedTile = thief.afterMinedTile;
                if (ironOrePrefab == null) ironOrePrefab = thief.ironOrePrefab;
            }
        }

        // 3. treeLayer が未設定（LayerMaskが空）の場合を補完する。
        // GameManager.SelectNextPlayer() が操作キャラクター昇格時に
        // AddComponent<PlayerAttack>() で新規追加すると、Inspectorで設定済みの
        // treeLayer はコピーされず空（value == 0）のまま生成されてしまい、
        // 木の当たり判定（Physics2D.OverlapCircleAll）が常に何もヒットしなくなる。
        if (treeLayer.value == 0)
        {
            AutoSetupTreeLayer();
        }
    }

    /// <summary>
    /// treeLayer を、シーン内に既に設定済みの PlayerAttack があればそこからコピーし、
    /// 見つからない場合は本プロジェクトのPlayerに設定されている木レイヤー
    /// （Layer 9 相当）をフォールバックとして使用する。
    /// </summary>
    private void AutoSetupTreeLayer()
    {
        PlayerAttack[] allAttacks = Object.FindObjectsByType<PlayerAttack>();
        foreach (PlayerAttack other in allAttacks)
        {
            if (other != this && other.treeLayer.value != 0)
            {
                treeLayer = other.treeLayer;
                return;
            }
        }

        // シーン内に参照できる設定が見つからない場合のフォールバック
        treeLayer = 1 << 9;
    }

    public void ResolveAttackPoint()
    {
        if (attackPoint != null)
        {
            axeSpriteRenderer = attackPoint.GetComponent<SpriteRenderer>();
            playerAxe = attackPoint.GetComponent<PlayerAxe>();
            hammer = attackPoint.GetComponent<Hammer>();

            if (playerAxe == null && hammer == null)
            {
                Debug.LogError("【要確認】attackPoint に PlayerAxe または Hammer スクリプトがアタッチされていません！");
            }
        }
    }

    public void SetAttackPoint(Transform newAttackPoint)
    {
        attackPoint = newAttackPoint;
        ResolveAttackPoint();
        AutoSetupMiningSettings(); // 武器セット時にも再自動取得
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        if (playerAxe != null)
        {
            playerAxe.ExecuteAttack();
        }
        else if (hammer != null)
        {
            hammer.ExecuteAttack();
        }

        // 木の伐採
        Collider2D[] hitTrees = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, treeLayer);
        foreach (Collider2D tree in hitTrees)
        {
            TreeController treeScript = tree.GetComponent<TreeController>();
            if (treeScript != null)
            {
                treeScript.TakeDamage(attackDamage);
            }
        }

        // 鉄鉱石の採掘（ツルハシ時）
        if (hammer != null)
        {
            TryMineIronTile();
        }

        yield return new WaitForSeconds(jumpDuration);
        isAttacking = false;
    }

    private void TryMineIronTile()
    {
        if (ironTilemap == null)
        {
            AutoSetupMiningSettings(); // 万が一取れていなければ再取得
            if (ironTilemap == null) return;
        }

        Vector3 center = attackPoint.position;
        float r = attackRadius;

        Vector3Int minCell = ironTilemap.WorldToCell(center - new Vector3(r, r, 0));
        Vector3Int maxCell = ironTilemap.WorldToCell(center + new Vector3(r, r, 0));

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);

                if (ironTilemap.HasTile(cellPos))
                {
                    Vector3 cellWorldPos = ironTilemap.GetCellCenterWorld(cellPos);
                    if (Vector2.Distance(center, cellWorldPos) <= attackRadius)
                    {
                        if (ironOrePrefab != null) Instantiate(ironOrePrefab, cellWorldPos, Quaternion.identity);
                        if (ResourceUIManager.Instance != null) ResourceUIManager.Instance.AddIron(1);

                        if (afterMinedTile != null) ironTilemap.SetTile(cellPos, afterMinedTile);
                        else ironTilemap.SetTile(cellPos, null);

                        Debug.Log($"⛏️ プレイヤーが位置 {cellPos} の鉄鉱石を採掘しました！");
                    }
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }
    }
}