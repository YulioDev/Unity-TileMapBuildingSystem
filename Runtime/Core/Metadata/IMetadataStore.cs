using UnityEngine;

namespace TMBS.Core.Metadata
{
    public interface IMetadataStore
    {
        bool TryGet(Vector3Int cell, out BuildRecord record);
        void Set(in BuildRecord record);
        void Remove(Vector3Int cell);
    }
}

