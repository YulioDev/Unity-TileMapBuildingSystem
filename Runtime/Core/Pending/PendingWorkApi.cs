using System;
using System.Collections.Generic;
using TMBS.Core.Events;
using TMBS.Core.History;
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
        private readonly bool _emitPendingEvents;
        private readonly bool _emitRegionEvents;
        private readonly Dictionary<int, List<TileChange>> _completedChangesByOrder = new Dictionary<int, List<TileChange>>();

        public IPendingStore Store => _store;

        public PendingWorkApi(
            IPendingStore store,
            IEventBus events,
            Tilemap targetTilemap,
            IMetadataStore metadata,
            string instanceId,
            bool allowPartialCompletion = true,
            ResourceRequirement resourcePolicy = ResourceRequirement.None,
            bool emitPendingEvents = true,
            bool emitRegionEvents = true)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _events = events;
            _targetTilemap = targetTilemap;
            _metadata = metadata;
            _instanceId = instanceId;
            _allowPartialCompletion = allowPartialCompletion;
            _resourcePolicy = resourcePolicy;
            _emitPendingEvents = emitPendingEvents;
            _emitRegionEvents = emitRegionEvents;
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

            if (_emitPendingEvents)
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

            if (_emitPendingEvents)
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
                        ApplyCompletedCell(pending.Id, pending.Bounds, in c);
                        pending.UpdateCell(i, c.WithCompleted());
                    }
                }

                pending.SetState(PendingState.Completed);
                _store.Remove(pending.Id);
                if (_emitPendingEvents)
                    _events?.Publish(new PendingCompletedEvent(_instanceId, pending.Id, pending.Bounds));
                return true;
            }

            ApplyCompletedCell(pending.Id, pending.Bounds, in cellData);
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
                if (_emitPendingEvents)
                    _events?.Publish(new PendingCompletedEvent(_instanceId, pending.Id, pending.Bounds));
            }
            else if (_emitPendingEvents)
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
            RevertCompletedOrderCells(pendingId);
            if (_emitPendingEvents)
            {
                _events?.Publish(new PendingChangedEvent(_instanceId, pendingId, PendingState.Cancelled));
                _events?.Publish(new PendingCompletedEvent(_instanceId, pendingId, bounds));
            }
            return true;
        }

        public bool RevertCompletedOrderCells(int orderId)
        {
            if (orderId <= 0 ||
                !_completedChangesByOrder.TryGetValue(orderId, out var changes) ||
                changes == null ||
                changes.Count == 0)
            {
                return false;
            }

            _completedChangesByOrder.Remove(orderId);

            for (int i = changes.Count - 1; i >= 0; i--)
            {
                var c = changes[i];
                if (_targetTilemap != null)
                {
                    _targetTilemap.SetTile(c.Cell, c.BeforeTile);
                    if (c.BeforeTile != null)
                    {
                        _targetTilemap.SetTileFlags(c.Cell, TileFlags.None);
                        _targetTilemap.SetTransformMatrix(c.Cell, c.BeforeMatrix);
                    }
                    else
                    {
                        _targetTilemap.SetTransformMatrix(c.Cell, Matrix4x4.identity);
                    }
                }

                if (_metadata != null)
                {
                    if (c.BeforeMeta.HasValue)
                        _metadata.Set(c.BeforeMeta.Value);
                    else
                        _metadata.Remove(c.Cell);
                }

                if (_emitRegionEvents)
                    _events?.Publish(new RegionModifiedEvent(_instanceId, new BoundsInt(c.Cell, Vector3Int.one)));
            }

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

        private void ApplyCompletedCell(int orderId, BoundsInt orderBounds, in PendingCell cell)
        {
            TileBase beforeTile = _targetTilemap != null ? _targetTilemap.GetTile(cell.Cell) : null;
            Matrix4x4 beforeMatrix = _targetTilemap != null ? _targetTilemap.GetTransformMatrix(cell.Cell) : Matrix4x4.identity;
            BuildRecord? beforeMeta = null;
            if (_metadata != null && _metadata.TryGet(cell.Cell, out var prevRec))
                beforeMeta = prevRec;

            BuildRecord? afterMeta = null;
            if (cell.Tile != null)
            {
                int maxHp = cell.BuildableData != null ? cell.BuildableData.maxHealth : 0;
                afterMeta = new BuildRecord(
                    cell.Tile,
                    cell.Cell,
                    BuildState.Completed,
                    cell.BuildableData,
                    orderBounds.position,
                    orderBounds,
                    maxHp);
            }

            if (_targetTilemap != null)
            {
                _targetTilemap.SetTile(cell.Cell, cell.Tile);
                if (cell.Tile != null)
                {
                    _targetTilemap.SetTileFlags(cell.Cell, TileFlags.None);
                    _targetTilemap.SetTransformMatrix(cell.Cell, cell.TransformMatrix);
                }
                else
                {
                    _targetTilemap.SetTransformMatrix(cell.Cell, Matrix4x4.identity);
                }
            }

            if (_metadata != null)
            {
                if (afterMeta.HasValue)
                    _metadata.Set(afterMeta.Value);
                else
                    _metadata.Remove(cell.Cell);
            }

            if (orderId > 0)
            {
                if (!_completedChangesByOrder.TryGetValue(orderId, out var list))
                {
                    list = new List<TileChange>(4);
                    _completedChangesByOrder[orderId] = list;
                }
                list.Add(new TileChange(
                    cell.Cell,
                    beforeTile,
                    cell.Tile,
                    beforeMeta,
                    afterMeta,
                    beforeMatrix,
                    cell.Tile != null ? cell.TransformMatrix : Matrix4x4.identity));
            }

            if (_emitRegionEvents)
                _events?.Publish(new RegionModifiedEvent(_instanceId, new BoundsInt(cell.Cell, Vector3Int.one)));
        }

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
