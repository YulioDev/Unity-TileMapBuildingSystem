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
        public readonly Vector3Int AnchorCell;
        public readonly BoundsInt FootprintBounds;
        public readonly int CurrentHealth;

        public BuildRecord(
            TileBase placedTile,
            Vector3Int cell,
            BuildState state,
            TmbsBuildableData buildableData = null)
            : this(
                placedTile,
                cell,
                state,
                buildableData,
                cell,
                new BoundsInt(cell, Vector3Int.one),
                buildableData != null ? buildableData.maxHealth : 0)
        {
        }

        public BuildRecord(
            TileBase placedTile,
            Vector3Int cell,
            BuildState state,
            TmbsBuildableData buildableData,
            Vector3Int anchorCell,
            BoundsInt footprintBounds,
            int currentHealth)
        {
            PlacedTile = placedTile;
            Cell = cell;
            State = state;
            BuildableData = buildableData;
            AnchorCell = anchorCell;
            FootprintBounds = footprintBounds;
            CurrentHealth = currentHealth;
        }
    }
}

