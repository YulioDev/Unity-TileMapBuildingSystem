using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public readonly struct TileCellWrite
    {
        public readonly Vector3Int Cell;
        public readonly TileBase Tile;

        public TileCellWrite(Vector3Int cell, TileBase tile)
        {
            Cell = cell;
            Tile = tile;
        }
    }

    public interface ITilemapWriteStrategy
    {
        void Write(Tilemap tilemap, BoundsInt bounds, TileBase[] tiles, Core.Validation.CellMask writeMask = null);
        void Write(Tilemap tilemap, IReadOnlyList<TileCellWrite> writes);
    }
}
