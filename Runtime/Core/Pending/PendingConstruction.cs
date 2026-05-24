using UnityEngine;

namespace TMBS.Core.Pending
{
    public sealed class PendingConstruction
    {
        public int Id { get; internal set; }
        public readonly string InstanceId;
        public readonly BoundsInt Bounds;

        private readonly PendingConstructionCell[] _cells;
        public PendingConstructionCell[] Cells => _cells;

        public PendingConstructionState State { get; private set; }

        public PendingConstruction(
            string instanceId,
            BoundsInt bounds,
            PendingConstructionCell[] cells,
            PendingConstructionState initialState)
        {
            InstanceId = instanceId;
            Bounds = bounds;
            _cells = cells;
            State = initialState;
        }

        public void SetState(PendingConstructionState state) => State = state;

        public void UpdateCell(int index, PendingConstructionCell updated)
        {
            if ((uint)index >= (uint)_cells.Length) return;
            _cells[index] = updated;
        }
    }
}