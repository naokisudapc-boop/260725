using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerFarming : MonoBehaviour
{
    [SerializeField] private Tilemap farmingTilemap;
    [SerializeField] private FarmTileData farmTileBase;

    [Header("Dynamic Interaction Settings")]
    [SerializeField] private float commandRange = 5.0f;

    [Header("Farming Key Settings")]
    [Tooltip("耕作コマンドのキー（左クリックは操作キャラクターになったBowManNPCの照準操作と衝突するため、キーボードのキーに変更）")]
    [SerializeField] private KeyCode plowKey = KeyCode.F;

    void Update()
    {
        // 耕作コマンド専用キーで「操作キャラクター自身の足元を耕す」
        if (Input.GetKeyDown(plowKey))
        {
            InteractWithTile(FarmTileData.TileStatus.Plowed);
        }

        // マウス右クリックで「NPCにそのマスの水やりを命令する」
        else if (Input.GetMouseButtonDown(1))
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f; // 2DなのでZ軸を平坦化
            Vector3Int gridPos = farmingTilemap.WorldToCell(mouseWorldPos);

            TileBase clickedTile = farmingTilemap.GetTile(gridPos);

            if (clickedTile != null && clickedTile is FarmTileData)
            {
                FarmTileData farmTile = (FarmTileData)clickedTile;

                if (farmTile.status == FarmTileData.TileStatus.Plowed)
                {
                    // ★修正：クリックしたマスの座標（mouseWorldPos）を渡してNPCを探す
                    FarmingNPC nearestNPC = FindNearestNPC(mouseWorldPos);
                    if (nearestNPC != null)
                    {
                        nearestNPC.AssignWateringTask(farmingTilemap, gridPos, farmTileBase);
                    }
                    else
                    {
                        Debug.LogWarning("[PlayerFarming] 範囲内に命令可能な FarmingNPC が見つかりませんでした。");
                    }
                }
                else
                {
                    Debug.Log("ここはすでに水が撒かれているか、耕されていないため水やりできません。");
                }
            }
            else
            {
                Debug.Log("ここには畑のタイルがありません。");
            }
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