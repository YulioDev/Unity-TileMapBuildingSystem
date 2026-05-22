using TMBS.Core.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.GridSpaces
{
    public sealed class UnityGridSpace : IGridSpace
    {
        private readonly Tilemap _tilemap;

        public UnityGridSpace(Tilemap tilemap)
        {
            if (tilemap == null)
                throw new System.ArgumentNullException(nameof(tilemap));

            _tilemap = tilemap;
        }

        public Vector3Int WorldToCell(Vector3 world) => _tilemap.WorldToCell(world);

        public Vector3 CellToWorld(Vector3Int cell) => _tilemap.GetCellCenterWorld(cell);
    }
}

