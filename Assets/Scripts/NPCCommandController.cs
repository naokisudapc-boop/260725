using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic; // HashSetを使用するために追加

public class NPCCommandController : MonoBehaviour
{
    [Header("Scan Target Tilemaps (multiple allowed)")]
    public Tilemap[] targetTilemaps;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M)) 
        {
            if (targetTilemaps == null || targetTilemaps.Length == 0)
            {
                Debug.LogWarning("Target Tilemaps are not registered!");
                return;
            }

            // 1. シーン内のすべての ThiefNPC を取得
            ThiefNPC[] allThieves = Object.FindObjectsByType<ThiefNPC>(FindObjectsSortMode.None);
            int successCount = 0;

            // 今回のMキー入力で、既に他のNPCが向かうことに決まった鉱石の座標を記憶するセット
            HashSet<Vector3Int> assignedTiles = new HashSet<Vector3Int>();

            // 2. すべてのNPCをループ走査
            foreach (ThiefNPC targetNPC in allThieves)
            {
                if (targetNPC == null) continue;

                // プレイヤー化しているキャラは除外
                if (targetNPC.gameObject.CompareTag("Player")) continue;

                // 男性NPCであるかどうかの判定
                bool isMale = targetNPC.gameObject.name.Contains("男") || 
                              targetNPC.gameObject.name.Contains("Male") || 
                              targetNPC.gender == Gender.Male;

                if (!isMale) continue;

                // ★拡張：すでに採掘中、または移動中の忙しいNPCはスキップしたい場合（もしThiefNPCにIsBusy等があれば条件に追加してください）
                // if (targetNPC.IsBusy) continue; 

                bool foundOreForThisNPC = false;

                // 3. このNPCの周囲にある鉱石を探索
                foreach (var tilemap in targetTilemaps)
                {
                    if (tilemap == null || foundOreForThisNPC) continue;

                    Vector3Int npcGridPos = tilemap.WorldToCell(targetNPC.transform.position);

                    for (int x = -10; x <= 10 && !foundOreForThisNPC; x++)
                    {
                        for (int y = -10; y <= 10 && !foundOreForThisNPC; y++)
                        {
                            for (int z = -3; z <= 3 && !foundOreForThisNPC; z++)
                            {
                                Vector3Int checkPos = new Vector3Int(npcGridPos.x + x, npcGridPos.y + y, npcGridPos.z + z);
                                
                                // すでにこのフレームで他のNPCが向かう予定の座標ならスキップ（重複防止）
                                if (assignedTiles.Contains(checkPos)) continue;

                                OreTileData oreTile = tilemap.GetTile<OreTileData>(checkPos);

                                if (oreTile != null)
                                {
                                    // このNPC用の鉱石が見つかったので指示を出す
                                    targetNPC.AssignMiningTask(tilemap, checkPos, null, true);
                                    Debug.Log($"【採掘指示成功】{targetNPC.name} を座標 {checkPos} へ向かわせました。");
                                    
                                    assignedTiles.Add(checkPos); // この座標を予約済みにする
                                    successCount++;
                                    foundOreForThisNPC = true; // このNPCの探索は終了し、次のNPCのチェックへ移る
                                }
                            }
                        }
                    }
                }
            }

            if (successCount > 0)
            {
                Debug.Log($"【一斉指示完了】合計 {successCount} 体の男性NPCに採掘指示を出しました！");
            }
            else
            {
                Debug.LogWarning("採掘可能な OreTileData が周囲にある男性NPCが見つかりませんでした。");
            }
        }
    }
}