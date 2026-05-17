using UnityEngine;

namespace TMBS.Core.Metadata
{
    public enum BuildState
    {
        Completed
    }

    public readonly struct BuildRecord
    {
        public readonly int BuildableId;
        public readonly Vector3Int Cell;
        public readonly BuildState State;

        public BuildRecord(int buildableId, Vector3Int cell, BuildState state)
        {
            BuildableId = buildableId;
            Cell = cell;
            State = state;
        }
    }
}

