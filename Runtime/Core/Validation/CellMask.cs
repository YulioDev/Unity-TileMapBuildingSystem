using System;
using TMBS.Core.Grid;
using UnityEngine;

namespace TMBS.Core.Validation
{
    
    
    
    
    public sealed class CellMask
    {
        public BoundsInt Bounds { get; }
        public bool[] Bits { get; }
        public int Length => Bits.Length;

        public CellMask(BoundsInt bounds, bool defaultValue)
        {
            Bounds = bounds;

            int len = TileBlockIndex.Volume(bounds);
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

            return TileBlockIndex.IndexOf(Bounds, cell);
        }

        public Vector3Int CellAt(int index)
        {
            return TileBlockIndex.CellAt(Bounds, index);
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