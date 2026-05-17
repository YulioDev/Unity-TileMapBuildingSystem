using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public interface ITilemapBatchWriter
    {
        void WriteBlock(Tilemap tilemap, BoundsInt bounds, TileBase[] tiles);
    }
}

