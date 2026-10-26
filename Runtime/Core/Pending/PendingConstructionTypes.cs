using System;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Pending
{
    public enum PendingState
    {
        WaitingForResources,
        ReadyToBuild,
        InProgress,
        Completed,
        Cancelled
    }

    public readonly struct PendingCell
    {
        public readonly Vector3Int Cell;
        public readonly TileBase Tile;
        public readonly TmbsBuildableData BuildableData;
        public readonly int WorkRequired;
        public readonly int WorkDone;
        public readonly int ResourceRequired;
        public readonly int ResourceDelivered;
        public readonly bool IsCompleted;
        public readonly Matrix4x4 TransformMatrix;

        public bool IsReadyForWork =>
            ResourceRequired == 0 || ResourceDelivered >= ResourceRequired;

        public bool IsWorkComplete =>
            WorkDone >= WorkRequired;

        public int WorkAllowed(ResourceRequirement policy)
        {
            if (ResourceRequired <= 0 || policy != ResourceRequirement.AllowPartialDelivery)
                return IsReadyForWork ? WorkRequired : 0;
            return WorkRequired * ResourceDelivered / ResourceRequired;
        }

        public PendingCell(
            Vector3Int cell,
            TileBase tile,
            int workRequired,
            int workDone,
            int resourceRequired,
            int resourceDelivered)
            : this(cell, tile, null, workRequired, workDone, resourceRequired, resourceDelivered, false, Matrix4x4.identity)
        {
        }

        public PendingCell(
            Vector3Int cell,
            TileBase tile,
            TmbsBuildableData buildableData,
            int workRequired,
            int workDone,
            int resourceRequired,
            int resourceDelivered)
            : this(cell, tile, buildableData, workRequired, workDone, resourceRequired, resourceDelivered, false, Matrix4x4.identity)
        {
        }

        public PendingCell(
            Vector3Int cell,
            TileBase tile,
            TmbsBuildableData buildableData,
            int workRequired,
            int workDone,
            int resourceRequired,
            int resourceDelivered,
            bool isCompleted)
            : this(cell, tile, buildableData, workRequired, workDone, resourceRequired, resourceDelivered, isCompleted, Matrix4x4.identity)
        {
        }

        public PendingCell(
            Vector3Int cell,
            TileBase tile,
            TmbsBuildableData buildableData,
            int workRequired,
            int workDone,
            int resourceRequired,
            int resourceDelivered,
            bool isCompleted,
            Matrix4x4 transformMatrix)
        {
            Cell = cell;
            Tile = tile;
            BuildableData = buildableData;
            WorkRequired = workRequired;
            WorkDone = workDone;
            ResourceRequired = resourceRequired;
            ResourceDelivered = resourceDelivered;
            IsCompleted = isCompleted;
            TransformMatrix = transformMatrix == default ? Matrix4x4.identity : transformMatrix;
        }

        public PendingCell WithWorkAdded(int amount) =>
            new PendingCell(
                Cell, Tile, BuildableData,
                WorkRequired,
                Math.Min(WorkDone + amount, WorkRequired),
                ResourceRequired, ResourceDelivered,
                IsCompleted, TransformMatrix);

        public PendingCell WithResourcesAdded(int amount) =>
            new PendingCell(
                Cell, Tile, BuildableData,
                WorkRequired, WorkDone,
                ResourceRequired,
                Math.Min(ResourceDelivered + amount, ResourceRequired),
                IsCompleted, TransformMatrix);

        public PendingCell WithCompleted() =>
            new PendingCell(
                Cell, Tile, BuildableData,
                WorkRequired, WorkDone,
                ResourceRequired, ResourceDelivered,
                true, TransformMatrix);
    }
}
