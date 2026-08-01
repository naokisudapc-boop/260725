using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Farm Tile", menuName = "Tiles/Farm Tile")]
public class FarmTileData : Tile
{
    // 鉱石（Mined）を削除し、畑のステートだけに絞る
    public enum TileStatus { Normal, Plowed, Watered }

    [Header("畑の状態")]
    public TileStatus status = TileStatus.Normal;

    [Header("見た目の画像")]
    public Sprite plowedSprite;
    public Sprite wateredSprite;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);

        // 状態に合わせて表示するスプライトを切り替える
        if (status == TileStatus.Plowed && plowedSprite != null)
        {
            tileData.sprite = plowedSprite;
        }
        else if (status == TileStatus.Watered && wateredSprite != null)
        {
            tileData.sprite = wateredSprite;
        }
    }
}