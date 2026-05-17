using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public readonly struct TilemapTarget
    {
        public readonly Tilemap Tilemap;

        public TilemapTarget(Tilemap tilemap)
        {
            Tilemap = tilemap;
        }
    }
}

