using System;
using System.Collections.Generic;
using TMBS.Core.Events;
using TMBS.Core.Metadata;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Pending
{
    public sealed class PendingWorkApi : IPendingWorkApi
    {
        private readonly IPendingStore _store;
        private readonly IEventBus _events;
        private readonly Tilemap _targetTilemap;
        private readonly IMetadataStore _metadata;
        private readonly string _instanceId;
        private readonly bool _allowPartialCompletion;
        private readonly ResourceRequirement _resourcePolicy;

        public IPendingStore Store => _store;

        public PendingWorkApi(
            IPendingStore store,
            IEventBus events,
            Tilemap targetTilemap,
            IMetadataStore metadata,
            string instanceId,
            bool allowPartialCompletion = true,
            ResourceRequirement resourcePolicy = ResourceRequirement.None)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _events = events;
            _targetTilemap = targetTilemap;
            _metadata = metadata;
            _instanceId = instanceId;
            _allowPartialCompletion = allowPartialCompletion;
            _resourcePolicy = resourcePolicy;
        }

        public bool TryDeliverResources(Vector3Int cell, int amount)
        {
            if (amount <= 0) return false;
            if (!_store.TryGetByCell(cell, out var pending, out int idx))
                return false;

            var cellData = pending.Cells[idx];
            if (cellData.IsCompleted)
                return false;

            var updated = cellData.WithResourcesAdded(amount);
            pending.UpdateCell(idx, updated);

            if (pending.State == PendingState.WaitingForResources)
            {
                bool canBuild = _resourcePolicy == ResourceRequirement.AllowPartialDelivery
                    ? (pending.TotalResourceDelivered > 0 || AllCellsReadyForWork(pending))
                    : AllCellsReadyForWork(pending);

                if (canBuild)
                {
                    pending.SetState(PendingState.ReadyToBuild);
                }
            }

            _events?.Publish(new PendingChangedEvent(_instanceId, pending.Id, pending.State));
            return true;
        }

        public bool TryAddWork(Vector3Int cell, int amount)
        {
            if (amount <= 0) return false;
            if (!_store.TryGetByCell(cell, out var pending, out int idx))
                return false;

            var cellData = pending.Cells[idx];
            if (cellData.IsCompleted)
                return false;

            int maxAllowed = cellData.WorkAllowed(_resourcePolicy);
            int availableWork = maxAllowed - cellData.WorkDone;
            if (availableWork <= 0)
                return false;

            int actualAmount = Math.Min(amount, availableWork);
            var updated = cellData.WithWorkAdded(actualAmount);
            pending.UpdateCell(idx, updated);

            if (pending.State == PendingState.ReadyToBuild || pending.State == PendingState.WaitingForResources)
                pending.SetState(PendingState.InProgress);

            _events?.Publish(new PendingChangedEvent(_instanceId, pending.Id, pending.State));

            return true;
        }

        public bool TryCompleteCell(Vector3Int cell)
        {
            if (!_store.TryGetByCell(cell, out var pending, out int idx))
                return false;

            var cellData = pending.Cells[idx];
            if (cellData.IsCompleted)
                return false;

            if (!cellData.IsWorkComplete)
                return false;

            if (!_allowPartialCompletion && pending.Cells.Length > 1)
            {
                var allCells = pending.Cells;
                for (int i = 0; i < allCells.Length; i++)
                {
                    if (!allCells[i].IsWorkComplete)
                        return false;
                }

                for (int i = 0; i < allCells.Length; i++)
                {
                    var c = allCells[i];
                    if (!c.IsCompleted)
                    {
                        if (_targetTilemap != null)
                        {
                            _targetTilemap.SetTile(c.Cell, c.Tile);
                            if (c.Tile != null)
                            {
                                _targetTilemap.SetTileFlags(c.Cell, TileFlags.None);
                                _targetTilemap.SetTransformMatrix(c.Cell, c.TransformMatrix);
                            }
                            else
                            {
                                _targetTilemap.SetTransformMatrix(c.Cell, Matrix4x4.identity);
                            }
                        }

                        if (_metadata != null)
                        {
                            if (c.Tile != null)
                                _metadata.Set(new BuildRecord(c.Tile, c.Cell, BuildState.Completed, c.BuildableData));
                            else
                                _metadata.Remove(c.Cell);
                        }

                        _events?.Publish(new RegionModifiedEvent(_instanceId, new BoundsInt(c.Cell, Vector3Int.one)));
                        pending.UpdateCell(i, c.WithCompleted());
                    }
                }

                pending.SetState(PendingState.Completed);
                _store.Remove(pending.Id);
                _events?.Publish(new PendingCompletedEvent(_instanceId, pending.Id, pending.Bounds));
                return true;
            }

            if (_targetTilemap != null)
            {
                _targetTilemap.SetTile(cell, cellData.Tile);
                if (cellData.Tile != null)
                {
                    _targetTilemap.SetTileFlags(cell, TileFlags.None);
                    _targetTilemap.SetTransformMatrix(cell, cellData.TransformMatrix);
                }
                else
                {
                    _targetTilemap.SetTransformMatrix(cell, Matrix4x4.identity);
                }
            }

            if (_metadata != null)
            {
                if (cellData.Tile != null)
                    _metadata.Set(new BuildRecord(cellData.Tile, cell, BuildState.Completed, cellData.BuildableData));
                else
                    _metadata.Remove(cell);
            }

            _events?.Publish(new RegionModifiedEvent(_instanceId, new BoundsInt(cell, Vector3Int.one)));
            pending.UpdateCell(idx, cellData.WithCompleted());

            bool allDone = true;
            var cells = pending.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].IsCompleted)
                {
                    allDone = false;
                    break;
                }
            }

            if (allDone)
            {
                pending.SetState(PendingState.Completed);
                _store.Remove(pending.Id);
                _events?.Publish(new PendingCompletedEvent(_instanceId, pending.Id, pending.Bounds));
            }
            else
            {
                _events?.Publish(new PendingChangedEvent(_instanceId, pending.Id, pending.State));
            }

            return true;
        }

        public bool TryCancel(int pendingId)
        {
            if (!_store.TryGet(pendingId, out var pending))
                return false;

            var bounds = pending.Bounds;
            pending.SetState(PendingState.Cancelled);
            _store.Remove(pendingId);
            _events?.Publish(new PendingChangedEvent(_instanceId, pendingId, PendingState.Cancelled));
            _events?.Publish(new PendingCompletedEvent(_instanceId, pendingId, bounds));
            return true;
        }

        public bool TryGetCellStatus(Vector3Int cell, out PendingCell status)
        {
            status = default;
            if (!_store.TryGetByCell(cell, out var pending, out int idx))
                return false;

            status = pending.Cells[idx];
            return true;
        }

        public IReadOnlyList<PendingOrder> GetAll() => _store.All;

        private static bool AllCellsReadyForWork(PendingOrder pending)
        {
            var cells = pending.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].IsCompleted && !cells[i].IsReadyForWork)
                    return false;
            }
            return true;
        }
    }
}
