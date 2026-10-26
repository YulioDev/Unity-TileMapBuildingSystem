using UnityEngine;

namespace TMBS.Core.Selection
{
    public struct PlacementTransform
    {
        public int Rotation;   // 0..3, pasos de 90°
        public bool Mirrored;

        public PlacementTransform(int rotation, bool mirrored)
        {
            Rotation = ((rotation % 4) + 4) % 4;
            Mirrored = mirrored;
        }

        public Matrix4x4 ToMatrix() =>
            Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, -90f * Rotation),
                          new Vector3(Mirrored ? -1f : 1f, 1f, 1f));

        public Vector2Int Apply(Vector2Int size) => (Rotation & 1) == 1 ? new Vector2Int(size.y, size.x) : size;
    }
}
