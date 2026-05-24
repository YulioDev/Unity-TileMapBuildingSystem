using System;
using System.Collections.Generic;
using TMBS.Core.Events;
using TMBS.Core.Metadata;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Pending
{
    public sealed class DefaultPendingConstructionWorkApi : IPendingConstructionWorkApi
    {
        private readonly IPendingConstructionStore _store;
        private readonly IEventBus _events;
        private readonly Tilemap _targetTilemap;
        private readonly IMetadataStore _metadata;
        private readonly string _instanceId;

        public DefaultPendingConstructionWorkApi(
            IPendingConstructionStore store,
            IEventBus events,
            Tilemap targetTilemap,
            IMetadataStore metadata,
            string instanceId)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _events = events;
            _targetTilemap = targetTilemap;
            _metadata = metadata;
            _instanceId = instanceId;
        }

        public bool TryDeliverResources(Vector3Int cell, int amount)
        {
            if (amount <= 0) return false;
            if (!_store.TryGetByCell(cell, out var pending, out int idx))
                return false;

            var updated = pending.Cells[idx].WithResourcesAdded(amount);
            pending.UpdateCell(idx, updated);

            if (pending.State == PendingConstructionState.WaitingForResources && updated.IsReadyForWork)
            {
                pending.SetState(PendingConstructionState.ReadyToBuild);
                _events?.Publish(new PendingConstructionChangedEvent(_instanceId, pending.Id, pending.State));
            }

            return true;
        }

        public bool TryAddWork(Vector3Int cell, int amount)
        {
            if (amount <= 0) return false;
            if (!_store.TryGetByCell(cell, out var pending, out int idx))
                return false;

            var cellData = pending.Cells[idx];
            if (!cellData.IsReadyForWork)
                return false;

            pending.UpdateCell(idx, cellData.WithWorkAdded(amount));
            if (pending.State != PendingConstructionState.InProgress)
            {
                pending.SetState(PendingConstructionState.InProgress);
                _events?.Publish(new PendingConstructionChangedEvent(_instanceId, pending.Id, pending.State));
            }

            return true;
        }

        public bool TryCompleteCell(Vector3Int cell)
        {
            if (!_store.TryGetByCell(cell, out var pending, out int idx))
                return false;

            var cellData = pending.Cells[idx];
            if (!cellData.IsWorkComplete)
                return false;

            if (_targetTilemap != null)
                _targetTilemap.SetTile(cell, cellData.Tile);

            if (_metadata != null && cellData.Tile != null)
                _metadata.Set(new BuildRecord(cellData.Tile, cell, BuildState.Completed));

            _events?.Publish(new RegionModifiedEvent(_instanceId, new BoundsInt(cell, Vector3Int.one)));

            bool allDone = true;
            var cells = pending.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].IsWorkComplete)
                {
                    allDone = false;
                    break;
                }
            }

            if (allDone)
            {
                pending.SetState(PendingConstructionState.Completed);
                _store.Remove(pending.Id);
                _events?.Publish(new PendingConstructionCompletedEvent(_instanceId, pending.Id, pending.Bounds));
            }
            else
            {
                _events?.Publish(new PendingConstructionChangedEvent(_instanceId, pending.Id, pending.State));
            }

            return true;
        }

        public bool TryCancel(int pendingId)
        {
            if (!_store.TryGet(pendingId, out var pending))
                return false;

            pending.SetState(PendingConstructionState.Cancelled);
            _store.Remove(pendingId);
            _events?.Publish(new PendingConstructionChangedEvent(_instanceId, pendingId, PendingConstructionState.Cancelled));
            return true;
        }

        public bool TryGetCellStatus(Vector3Int cell, out PendingConstructionCell status)
        {
            status = default;
            if (!_store.TryGetByCell(cell, out var pending, out int idx))
                return false;

            status = pending.Cells[idx];
            return true;
        }

        public IReadOnlyList<PendingConstruction> GetAll() => _store.All;
    }
}