using TMBS.Runtime.Config;
using UnityEngine;

namespace TMBS.Core.Pending
{
    public sealed class PendingOrder
    {
        public int Id { get; internal set; }
        public readonly string InstanceId;
        public readonly BoundsInt Bounds;

        private readonly PendingCell[] _cells;
        public PendingCell[] Cells => _cells;

        public PendingState State { get; private set; }

        public int TotalWorkRequired
        {
            get
            {
                if (_cells == null) return 0;
                int total = 0;
                for (int i = 0; i < _cells.Length; i++)
                    total += _cells[i].WorkRequired;
                return total;
            }
        }

        public int TotalWorkDone
        {
            get
            {
                if (_cells == null) return 0;
                int total = 0;
                for (int i = 0; i < _cells.Length; i++)
                    total += _cells[i].WorkDone;
                return total;
            }
        }

        public int TotalResourceRequired
        {
            get
            {
                if (_cells == null) return 0;
                int total = 0;
                for (int i = 0; i < _cells.Length; i++)
                    total += _cells[i].ResourceRequired;
                return total;
            }
        }

        public int TotalResourceDelivered
        {
            get
            {
                if (_cells == null) return 0;
                int total = 0;
                for (int i = 0; i < _cells.Length; i++)
                    total += _cells[i].ResourceDelivered;
                return total;
            }
        }

        public float NormalizedProgress
        {
            get
            {
                int totalReq = TotalWorkRequired + TotalResourceRequired;
                if (totalReq <= 0) return 1f;
                return Mathf.Clamp01((float)(TotalWorkDone + TotalResourceDelivered) / totalReq);
            }
        }

        public TmbsBuildableData PrimaryBuildableData
        {
            get
            {
                if (_cells == null || _cells.Length == 0) return null;
                for (int i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i].BuildableData != null)
                        return _cells[i].BuildableData;
                }
                return null;
            }
        }

        public PendingOrder(
            string instanceId,
            BoundsInt bounds,
            PendingCell[] cells,
            PendingState initialState)
        {
            InstanceId = instanceId;
            Bounds = bounds;
            _cells = cells;
            State = initialState;
        }

        public void SetState(PendingState state) => State = state;

        public void UpdateCell(int index, PendingCell updated)
        {
            if ((uint)index >= (uint)_cells.Length) return;
            _cells[index] = updated;
        }
    }
}
