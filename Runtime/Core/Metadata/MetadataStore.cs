using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Metadata
{
    public sealed class MetadataStore : IMetadataStore
    {
        private readonly Dictionary<Vector3Int, BuildRecord> _byCell;
        private readonly Queue<Vector3Int> _insertionOrder;
        private readonly int _capacity;

        public int Count => _byCell.Count;

        public MetadataStore(int capacity = 1024)
        {
            _capacity = capacity > 0 ? capacity : 0;
            _byCell = new Dictionary<Vector3Int, BuildRecord>(_capacity > 0 ? _capacity : 1024);
            _insertionOrder = new Queue<Vector3Int>(_capacity > 0 ? _capacity : 16);
        }

        public bool TryGet(Vector3Int cell, out BuildRecord record) => _byCell.TryGetValue(cell, out record);

        public void Set(in BuildRecord record)
        {
            bool exists = _byCell.ContainsKey(record.Cell);
            if (!exists && _capacity > 0)
            {
                while (_byCell.Count >= _capacity && _insertionOrder.Count > 0)
                {
                    var oldest = _insertionOrder.Dequeue();
                    _byCell.Remove(oldest);
                }
            }

            _byCell[record.Cell] = record;
            if (!exists && _capacity > 0)
                _insertionOrder.Enqueue(record.Cell);
        }

        public void Remove(Vector3Int cell) => _byCell.Remove(cell);

        public IReadOnlyCollection<BuildRecord> GetAll() => _byCell.Values;

        public void Clear()
        {
            _byCell.Clear();
            _insertionOrder.Clear();
        }
    }
}
