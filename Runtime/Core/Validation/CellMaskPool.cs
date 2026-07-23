using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Validation
{
    public static class CellMaskPool
    {
        private static readonly List<CellMask> _activeMasks = new List<CellMask>(32);
        private static readonly Stack<CellMask> _pool = new Stack<CellMask>(32);

        public static CellMask Rent(BoundsInt bounds, bool defaultValue)
        {
            CellMask mask;
            if (_pool.Count > 0)
            {
                mask = _pool.Pop();
                mask.Reset(bounds, defaultValue);
            }
            else
            {
                mask = new CellMask(bounds, defaultValue);
            }
            _activeMasks.Add(mask);
            return mask;
        }

        /// <summary>
        /// Call this once per frame (e.g., in BuildeableTilemap.Update) to return all 
        /// rented masks to the pool. This avoids the need for complex lifetime tracking.
        /// </summary>
        public static void ReclaimAll()
        {
            for (int i = 0; i < _activeMasks.Count; i++)
            {
                _pool.Push(_activeMasks[i]);
            }
            _activeMasks.Clear();
        }

        public static void Clear()
        {
            _pool.Clear();
            _activeMasks.Clear();
        }
    }
}
