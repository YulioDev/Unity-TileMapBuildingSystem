using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public sealed class TileArrayBuilder
    {
        public TileBase[] FillBlock(BoundsInt bounds, TileBase tile)
        {
            int len = bounds.size.x * bounds.size.y * bounds.size.z;
            var arr = new TileBase[len];
            for (int i = 0; i < len; i++) arr[i] = tile;
            return arr;
        }

        public TileBase[] ReadBlock(Tilemap tilemap, BoundsInt bounds)
        {
            return tilemap.GetTilesBlock(bounds);
        }
    }
}
