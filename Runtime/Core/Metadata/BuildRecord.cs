using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Metadata
{
    public enum BuildState
    {
        Completed
    }

    public readonly struct BuildRecord
    {
        public readonly TileBase PlacedTile;
        public readonly Vector3Int Cell;
        public readonly BuildState State;

        public BuildRecord(TileBase placedTile, Vector3Int cell, BuildState state)
        {
            PlacedTile = placedTile;
            Cell       = cell;
            State      = state;
        }
    }
}

