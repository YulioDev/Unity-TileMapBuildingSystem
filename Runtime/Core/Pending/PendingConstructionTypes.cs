using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Pending
{
    public enum PendingConstructionState
    {
        Planned,
        WaitingForResources,
        ReadyToBuild,
        InProgress,
        Completed,
        Cancelled
    }

    public readonly struct PendingConstructionCell
    {
        public readonly Vector3Int Cell;
        public readonly TileBase Tile;
        public readonly int WorkRequired;
        public readonly int WorkDone;
        public readonly int ResourceRequired;
        public readonly int ResourceDelivered;

        public bool IsReadyForWork =>
            ResourceRequired == 0 || ResourceDelivered >= ResourceRequired;

        public bool IsWorkComplete =>
            WorkDone >= WorkRequired;

        public PendingConstructionCell(
            Vector3Int cell,
            TileBase tile,
            int workRequired,
            int workDone,
            int resourceRequired,
            int resourceDelivered)
        {
            Cell = cell;
            Tile = tile;
            WorkRequired = workRequired;
            WorkDone = workDone;
            ResourceRequired = resourceRequired;
            ResourceDelivered = resourceDelivered;
        }

        public PendingConstructionCell WithWorkAdded(int amount) =>
            new PendingConstructionCell(
                Cell, Tile,
                WorkRequired,
                Math.Min(WorkDone + amount, WorkRequired),
                ResourceRequired, ResourceDelivered);

        public PendingConstructionCell WithResourcesAdded(int amount) =>
            new PendingConstructionCell(
                Cell, Tile,
                WorkRequired, WorkDone,
                ResourceRequired,
                Math.Min(ResourceDelivered + amount, ResourceRequired));
    }
}