using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Metadata
{
    public sealed class DefaultMetadataStore : IMetadataStore
    {
        private readonly Dictionary<Vector3Int, BuildRecord> _byCell;

        public DefaultMetadataStore(int capacity = 1024)
        {
            _byCell = new Dictionary<Vector3Int, BuildRecord>(capacity);
        }

        public bool TryGet(Vector3Int cell, out BuildRecord record) => _byCell.TryGetValue(cell, out record);

        public void Set(in BuildRecord record) => _byCell[record.Cell] = record;

        public void Remove(Vector3Int cell) => _byCell.Remove(cell);
    }
}

