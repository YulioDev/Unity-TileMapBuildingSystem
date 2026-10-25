using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Metadata
{
    public enum BuildState
    {
        Pending,
        InProgress,
        Completed,
        Cancelled
    }

    public readonly struct BuildRecord
    {
        public readonly TileBase PlacedTile;
        public readonly Vector3Int Cell;
        public readonly BuildState State;
        public readonly TmbsBuildableData BuildableData;

        public BuildRecord(TileBase placedTile, Vector3Int cell, BuildState state, TmbsBuildableData buildableData = null)
        {
            PlacedTile = placedTile;
            Cell       = cell;
            State      = state;
            BuildableData = buildableData;
        }
    }
}

