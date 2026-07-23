using TMBS.Core.Validation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public sealed class UnityTilemapOccupancySource : ICellOccupancySource
    {
        private readonly Tilemap _tilemap;

        public UnityTilemapOccupancySource(Tilemap tilemap)
        {
            if (tilemap == null) throw new System.ArgumentNullException(nameof(tilemap));
            _tilemap = tilemap;
        }

        public bool HasTile(Vector3Int cell)
        {
            return _tilemap != null && _tilemap.HasTile(cell);
        }
    }
}
