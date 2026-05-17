using System;
using UnityEngine;

namespace TMBS.Core.Validation
{
    /// <summary>
    /// Máscara booleana sobre un BoundsInt. Bits[i] corresponde a una celda dentro de Bounds.
    /// Semántica: el llamador decide qué significa "true" (bloqueado / escribible / etc.).
    /// </summary>
    public sealed class CellMask
    {
        public BoundsInt Bounds { get; }
        public bool[] Bits { get; }
        public int Length => Bits.Length;

        private readonly int _w;
        private readonly int _h;
        private readonly int _d;
        private readonly Vector3Int _origin;

        public CellMask(BoundsInt bounds, bool defaultValue)
        {
            Bounds = bounds;
            _origin = bounds.position;
            _w = Math.Max(0, bounds.size.x);
            _h = Math.Max(0, bounds.size.y);
            _d = Math.Max(0, bounds.size.z);

            int len = _w * _h * _d;
            Bits = new bool[len];

            if (defaultValue)
            {
                for (int i = 0; i < len; i++) Bits[i] = true;
            }
        }

        public static CellMask AllTrue(BoundsInt bounds) => new CellMask(bounds, true);
        public static CellMask AllFalse(BoundsInt bounds) => new CellMask(bounds, false);

        public bool Contains(Vector3Int cell) => Bounds.Contains(cell);

        public int IndexOf(Vector3Int cell)
        {
            if (!Bounds.Contains(cell))
                throw new InvalidOperationException("CellMask.IndexOf called with a cell outside of Bounds.");

            int x = cell.x - _origin.x;
            int y = cell.y - _origin.y;
            int z = cell.z - _origin.z;

            return (z * _h + y) * _w + x;
        }

        public Vector3Int CellAt(int index)
        {
            int xy = _w * _h;
            int z = index / xy;
            int rem = index - (z * xy);
            int y = rem / _w;
            int x = rem - (y * _w);
            return new Vector3Int(_origin.x + x, _origin.y + y, _origin.z + z);
        }

        public bool AnyTrue()
        {
            for (int i = 0; i < Bits.Length; i++)
                if (Bits[i]) return true;
            return false;
        }

        public void OrInPlace(CellMask other)
        {
            EnsureSameBounds(other);
            for (int i = 0; i < Bits.Length; i++)
                Bits[i] = Bits[i] || other.Bits[i];
        }

        public void AndInPlace(CellMask other)
        {
            EnsureSameBounds(other);
            for (int i = 0; i < Bits.Length; i++)
                Bits[i] = Bits[i] && other.Bits[i];
        }

        private void EnsureSameBounds(CellMask other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (other.Bounds.position != Bounds.position || other.Bounds.size != Bounds.size)
                throw new InvalidOperationException("CellMask bounds mismatch. All masks must target the same operation BoundsInt.");
        }
    }
}