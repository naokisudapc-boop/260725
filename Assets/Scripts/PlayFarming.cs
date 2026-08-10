using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerFarming : MonoBehaviour
{
    [SerializeField] private Tilemap farmingTilemap;
    [SerializeField] private FarmTileData farmTileBase;

    [Header("Dynamic Interaction Settings")]
    [SerializeField] private float commandRange = 5.0f;

    [Header("Farming Key Settings")]
    [Tooltip("耕作コマンドのキー")]
    [SerializeField] private KeyCode plowKey = KeyCode.F;
    [Tooltip("水やり指示のキー。操作キャラクターの足元の耕作済み畑を対象にする")]
    [SerializeField] private KeyCode wateringKey = KeyCode.G;

void Update()
    {
        // 耕作コマンド専用キーで「操作キャラクター自身の足元を耕す」
        if (Input.GetKeyDown(plowKey))
        {
            InteractWithTile(FarmTileData.TileStatus.Plowed);
        }
        // 水やり指示は、操作キャラクターの足元にある耕作済みタイルを対象にする
        else if (Input.GetKeyDown(wateringKey))
        {
            IssueWateringCommand();
        }
    }

    // 操作キャラクターの足元の耕作済みタイルへ水やりを指示する
    private void IssueWateringCommand()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        Vector3Int gridPos = farmingTilemap.WorldToCell(playerObj.transform.position);
        TileBase tile = farmingTilemap.GetTile(gridPos);
        if (!(tile is FarmTileData farmTile) || farmTile.status != FarmTileData.TileStatus.Plowed)
        {
            Debug.Log("操作キャラクターの足元に水やり可能な耕作済み畑タイルがありません。");
            return;
        }

        Vector3 targetPosition = farmingTilemap.GetCellCenterWorld(gridPos);
        FarmingNPC nearestNPC = FindNearestNPC(targetPosition);
        if (nearestNPC != null)
        {
            nearestNPC.AssignWateringTask(farmingTilemap, gridPos, farmTileBase);
        }
        else
        {
            Debug.LogWarning("[PlayerFarming] 範囲内に命令可能な FarmingNPC が見つかりませんでした。");
        }
    }

    // 範囲内で最も近い FarmingNPC を検出する
    private FarmingNPC FindNearestNPC(Vector3 targetPosition)
    {
        FarmingNPC[] allNPCs = Object.FindObjectsByType<FarmingNPC>();
        
        FarmingNPC bestTarget = null;
        bool foundClone = false;
        float minDistance = commandRange;

        // Step1: クローン（生まれた女性NPC）を優先的に探す
        foreach (FarmingNPC npc in allNPCs)
        {
            if (npc.gender != Gender.Female) continue;
            if (!npc.gameObject.name.Contains("_Clone_")) continue;
            if (!npc.CanAcceptNewTask) continue; // すでに土地に固定されたNPCは除外

            // ★修正：クリックした位置（targetPosition）からの距離を測る
            float distance = Vector2.Distance(targetPosition, npc.transform.position);
            if (distance <= commandRange)
            {
                bestTarget = npc;
                foundClone = true;
                break;
            }
        }

        // Step2: 通常の初期配置女性NPCを探す
        if (!foundClone)
        {
            foreach (FarmingNPC npc in allNPCs)
            {
                if (npc.gender != Gender.Female) continue;
                if (npc.gameObject.name.Contains("_Clone_")) continue;
                if (!npc.CanAcceptNewTask) continue; // すでに土地に固定されたNPCは除外

                // ★修正：クリックした位置（targetPosition）からの距離を測る
                float distance = Vector2.Distance(targetPosition, npc.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestTarget = npc;
                }
            }
        }

        return bestTarget;
    }

    // 耕す処理（操作キャラクター自身の位置のマスを耕す）
    void InteractWithTile(FarmTileData.TileStatus newStatus)
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        Vector3Int gridPos = farmingTilemap.WorldToCell(playerObj.transform.position);

        FarmTileData newTile = ScriptableObject.CreateInstance<FarmTileData>();
        newTile.plowedSprite = farmTileBase.plowedSprite;
        newTile.wateredSprite = farmTileBase.wateredSprite;
        newTile.status = newStatus;

        farmingTilemap.SetTile(gridPos, newTile);
    }
}