using UnityEngine;

namespace TMBS.Core.Metadata
{
    public interface IPlacedConstruction
    {
        int BuildableId { get; }
        Vector3Int Cell { get; }
        BuildState State { get; }
    }
}

