using TMBS.Core.Pending;
using TMBS.Core.Selection;
using TMBS.Core.Validation;
using UnityEngine;

namespace TMBS.Core.Events
{
    public readonly struct BuildSelectionChangedEvent
    {
        public readonly string InstanceId;
        public readonly BuildSelectionData Selection;

        public BuildSelectionChangedEvent(string instanceId, BuildSelectionData selection)
        {
            InstanceId = instanceId;
            Selection  = selection;
        }
    }

    public readonly struct ValidationFailedEvent
    {
        public readonly string InstanceId;
        public readonly ValidationFailure Failure;
        public readonly Vector3Int Cell;
        public readonly BoundsInt OperationBounds;
        public readonly CellMask BlockedMask;

        public ValidationFailedEvent(string instanceId, ValidationFailure failure, Vector3Int cell, BoundsInt operationBounds = default, CellMask blockedMask = null)
        {
            InstanceId = instanceId;
            Failure = failure;
            Cell = cell;
            OperationBounds = operationBounds;
            BlockedMask = blockedMask;
        }
    }

    public readonly struct RegionModifiedEvent
    {
        public readonly string InstanceId;
        public readonly BoundsInt Bounds;

        public RegionModifiedEvent(string instanceId, BoundsInt bounds)
        {
            InstanceId = instanceId;
            Bounds = bounds;
        }
    }

    public readonly struct PendingConstructionCreatedEvent
    {
        public readonly string InstanceId;
        public readonly int PendingId;
        public readonly BoundsInt Bounds;

        public PendingConstructionCreatedEvent(string instanceId, int pendingId, BoundsInt bounds)
        {
            InstanceId = instanceId;
            PendingId = pendingId;
            Bounds = bounds;
        }
    }

    public readonly struct PendingConstructionChangedEvent
    {
        public readonly string InstanceId;
        public readonly int PendingId;
        public readonly PendingConstructionState State;

        public PendingConstructionChangedEvent(string instanceId, int pendingId, PendingConstructionState state)
        {
            InstanceId = instanceId;
            PendingId = pendingId;
            State = state;
        }
    }

    public readonly struct PendingConstructionCompletedEvent
    {
        public readonly string InstanceId;
        public readonly int PendingId;
        public readonly BoundsInt Bounds;

        public PendingConstructionCompletedEvent(string instanceId, int pendingId, BoundsInt bounds)
        {
            InstanceId = instanceId;
            PendingId = pendingId;
            Bounds = bounds;
        }
    }
}

