using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Metadata
{
    public interface IMetadataStore
    {
        int Count { get; }
        bool TryGet(Vector3Int cell, out BuildRecord record);
        void Set(in BuildRecord record);
        void Remove(Vector3Int cell);
        IReadOnlyCollection<BuildRecord> GetAll();
        void Clear();
    }
}

