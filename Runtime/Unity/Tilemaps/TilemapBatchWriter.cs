using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public sealed class TilemapBatchWriter : ITilemapBatchWriter
    {
        private readonly TileArrayBuilder _builder;

        public TilemapBatchWriter(TileArrayBuilder builder)
        {
            _builder = builder;
        }

        public void WriteBlock(Tilemap tilemap, BoundsInt bounds, TileBase[] tiles)
        {
            tilemap.SetTilesBlock(bounds, tiles);
        }
    }
}

