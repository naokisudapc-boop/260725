using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Ore Tile", menuName = "Tiles/Ore Tile")]
public class OreTileData : Tile
{
    [Header("鉱石タイルの見た目")]
    public Sprite minedSprite;

    // タイルが描画されるときにスプライトを設定
    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        if (minedSprite != null)
        {
            tileData.sprite = minedSprite;
        }
    }
}