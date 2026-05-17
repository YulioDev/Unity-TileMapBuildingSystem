using UnityEngine;

namespace TMBS.Core.Grid
{
    public interface IGridSpace
    {
        Vector3Int WorldToCell(Vector3 world);
        Vector3 CellToWorld(Vector3Int cell);
    }
}

