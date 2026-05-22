using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public sealed class TilemapBatchWriter : ITilemapBatchWriter
    {
        public TilemapBatchWriter()
        {
        }

        public void WriteBlock(Tilemap tilemap, BoundsInt bounds, TileBase[] tiles)
        {
            if (tilemap == null)
            {
                Debug.LogError("TMBS: Tilemap nulo en WriteBlock.");
                return;
            }

            int expected = bounds.size.x * bounds.size.y * bounds.size.z;

            if (expected < 0 || tiles == null || tiles.Length != expected)
            {
                Debug.LogError($"TMBS: Tile array inválido. Expected={expected}, Actual={tiles?.Length ?? -1}");
                return;
            }

            tilemap.SetTilesBlock(bounds, tiles);
        }
    }
}

