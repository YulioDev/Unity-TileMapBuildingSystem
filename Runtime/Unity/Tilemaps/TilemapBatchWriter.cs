using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public sealed class TilemapBatchWriter
    {
        public void Write(Tilemap tilemap, IReadOnlyList<TileCellWrite> writes)
        {
            if (tilemap == null || writes == null || writes.Count == 0)
                return;

            var positions = new Vector3Int[writes.Count];
            var tiles = new TileBase[writes.Count];

            for (int i = 0; i < writes.Count; i++)
            {
                positions[i] = writes[i].Cell;
                tiles[i] = writes[i].Tile;
            }

            tilemap.SetTiles(positions, tiles);
        }
    }

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
}