using TMBS.Core.Grid;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.GridSpaces
{
    public sealed class UnityGridSpace : IGridSpace
    {
        // Main tilemap reference.
        private readonly Tilemap _tilemap;

        public UnityGridSpace(Tilemap tilemap)
        {
            if (tilemap == null)
            {
                UnityEngine.Debug.LogError("UnityGridSpace: Tilemap cannot be a null reference in the constructor.");
                return;
            }
            _tilemap = tilemap;
        }

        public Vector3Int WorldToCell(Vector3 world) => _tilemap.WorldToCell(world);

        public Vector3 CellToWorld(Vector3Int cell) => _tilemap.GetCellCenterWorld(cell);
    }
}

