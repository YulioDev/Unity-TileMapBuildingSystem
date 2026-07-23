using System;
using TMBS.Core.Grid;
using UnityEngine;

namespace TMBS.Core.Validation
{
    
    
    
    
    public sealed class CellMask
    {
        public BoundsInt Bounds { get; private set; }
        public bool[] Bits { get; private set; }
        public int Length { get; private set; }

        public CellMask(BoundsInt bounds, bool defaultValue)
        {
            Reset(bounds, defaultValue);
        }

        public void Reset(BoundsInt bounds, bool defaultValue)
        {
            Bounds = bounds;
            Length = TileBlockIndex.Volume(bounds);

            if (Bits == null || Bits.Length < Length)
            {
                Bits = new bool[Math.Max(Length, 64)]; // Min capacity 64
            }

            if (defaultValue)
            {
                for (int i = 0; i < Length; i++) Bits[i] = true;
            }
            else
            {
                Array.Clear(Bits, 0, Length);
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
            for (int i = 0; i < Length; i++)
                if (Bits[i]) return true;
            return false;
        }

        public CellMask Clone()
        {
            var clone = new CellMask(Bounds, false);
            Array.Copy(Bits, clone.Bits, Length);
            return clone;
        }

        public void OrInPlace(CellMask other)
        {
            EnsureSameBounds(other);
            for (int i = 0; i < Length; i++)
                Bits[i] = Bits[i] || other.Bits[i];
        }

        public void AndInPlace(CellMask other)
        {
            EnsureSameBounds(other);
            for (int i = 0; i < Length; i++)
                Bits[i] = Bits[i] && other.Bits[i];
        }

        private void EnsureSameBounds(CellMask other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            if (other.Bounds.position != Bounds.position || other.Bounds.size != Bounds.size)
                throw new InvalidOperationException($"CellMask bounds mismatch. Expected {Bounds}, got {other.Bounds}.");
        }
    }
}