using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Pending
{
    public sealed class DefaultPendingConstructionStore : IPendingConstructionStore
    {
        private readonly Dictionary<int, PendingConstruction> _byId = new Dictionary<int, PendingConstruction>(64);
        private readonly Dictionary<Vector3Int, int> _cellToId = new Dictionary<Vector3Int, int>(256);
        private int _nextId = 1;

        public IReadOnlyList<PendingConstruction> All
        {
            get
            {
                var list = new List<PendingConstruction>(_byId.Count);
                foreach (var kv in _byId)
                    list.Add(kv.Value);
                return list;
            }
        }

        public int Add(PendingConstruction pending)
        {
            int id = _nextId++;
            pending.Id = id;

            _byId[id] = pending;

            for (int i = 0; i < pending.Cells.Length; i++)
                _cellToId[pending.Cells[i].Cell] = id;

            return id;
        }

        public bool TryGet(int id, out PendingConstruction pending) =>
            _byId.TryGetValue(id, out pending);

        public bool TryGetByCell(Vector3Int cell, out PendingConstruction pending, out int cellIndex)
        {
            pending = null;
            cellIndex = -1;

            if (!_cellToId.TryGetValue(cell, out int id))
                return false;

            if (!_byId.TryGetValue(id, out pending))
                return false;

            var cells = pending.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Cell == cell)
                {
                    cellIndex = i;
                    return true;
                }
            }

            return false;
        }

        public bool Remove(int id)
        {
            if (!_byId.TryGetValue(id, out var pending))
                return false;

            var cells = pending.Cells;
            for (int i = 0; i < cells.Length; i++)
                _cellToId.Remove(cells[i].Cell);

            _byId.Remove(id);
            return true;
        }
    }
}