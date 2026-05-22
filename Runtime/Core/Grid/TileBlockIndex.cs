using UnityEngine;

namespace TMBS.Core.Grid
{
    public static class TileBlockIndex
    {
        public static int Volume(BoundsInt bounds)
        {
            return bounds.size.x * bounds.size.y * bounds.size.z;
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
            int x = cell.x - bounds.position.x;
            int y = cell.y - bounds.position.y;
            int z = cell.z - bounds.position.z;

            return (z * bounds.size.y + y) * bounds.size.x + x;
        }
    }
}
