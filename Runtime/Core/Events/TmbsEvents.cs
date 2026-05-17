using TMBS.Core.Validation;
using UnityEngine;

namespace TMBS.Core.Events
{
    public readonly struct ValidationFailedEvent
    {
        public readonly string InstanceId;
        public readonly ValidationFailure Failure;
        public readonly Vector3Int Cell;

        public ValidationFailedEvent(string instanceId, ValidationFailure failure, Vector3Int cell)
        {
            InstanceId = instanceId;
            Failure = failure;
            Cell = cell;
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
}

