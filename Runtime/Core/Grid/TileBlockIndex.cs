using System;
using UnityEngine;

namespace TMBS.Core.Grid
{
    public static class TileBlockIndex
    {
        public static int Volume(BoundsInt bounds)
        {
            long volume = (long)bounds.size.x * bounds.size.y * bounds.size.z;
            if (bounds.size.x < 0 || bounds.size.y < 0 || bounds.size.z < 0 || volume > int.MaxValue)
                throw new InvalidOperationException("TMBS: Bounds volume is invalid or too large.");
            return (int)volume;
        }

        public static Vector3Int CellAt(BoundsInt bounds, int index)
        {
            int w = bounds.size.x;
            int h = bounds.size.y;
            int xy = w * h;

            int z = index / xy;
            int rem = index - z * xy;
            int y = rem / w;
            int x = rem - y * w;

            return new Vector3Int(
                bounds.position.x + x,
                bounds.position.y + y,
                bounds.position.z + z);
        }

        public static int IndexOf(BoundsInt bounds, Vector3Int cell)
        {
            long x = cell.x - bounds.position.x;
            long y = cell.y - bounds.position.y;
            long z = cell.z - bounds.position.z;
            long index = (z * bounds.size.y + y) * bounds.size.x + x;

            if (index < 0 || index > int.MaxValue)
                throw new InvalidOperationException("TMBS: Computed tile index is invalid or too large.");

            return (int)index;
        }
    }
}
