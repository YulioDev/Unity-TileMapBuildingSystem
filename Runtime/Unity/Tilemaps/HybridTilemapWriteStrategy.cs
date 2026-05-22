using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public sealed class HybridTilemapWriteStrategy : ITilemapWriteStrategy
    {
        private readonly ITilemapBatchWriter _batchWriter;
        private readonly float _denseThreshold;

        public HybridTilemapWriteStrategy(
            ITilemapBatchWriter batchWriter,
            float denseThreshold = 0.65f)
        {
            _batchWriter = batchWriter;
            _denseThreshold = denseThreshold;
        }

        public void Write(Tilemap tilemap, BoundsInt bounds, TileBase[] tiles, Core.Validation.CellMask writeMask = null)
        {
            if (writeMask == null)
            {
                _batchWriter.WriteBlock(tilemap, bounds, tiles);
                return;
            }

            int writable = CountTrue(writeMask.Bits);
            float density = writable / (float)writeMask.Bits.Length;

            if (density >= _denseThreshold)
            {
                _batchWriter.WriteBlock(tilemap, bounds, tiles);
                return;
            }

            for (int i = 0; i < writeMask.Bits.Length; i++)
            {
                if (!writeMask.Bits[i])
                    continue;

                tilemap.SetTile(writeMask.CellAt(i), tiles[i]);
            }
        }

        public void Write(Tilemap tilemap, IReadOnlyList<TileCellWrite> writes)
        {
            if (tilemap == null || writes == null)
                return;

            for (int i = 0; i < writes.Count; i++)
            {
                tilemap.SetTile(writes[i].Cell, writes[i].Tile);
            }
        }

        private static int CountTrue(bool[] bits)
        {
            int count = 0;

            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i])
                    count++;
            }

            return count;
        }
    }
}
