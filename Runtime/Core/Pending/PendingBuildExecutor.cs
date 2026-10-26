using System;
using TMBS.Core.Events;
using TMBS.Core.Execution;
using TMBS.Core.Pipeline;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Pending
{
    public sealed class PendingBuildExecutor : IBuildExecutor
    {
        private readonly IPendingStore _store;
        private readonly IEventBus _events;
        private readonly TmbsPendingConfig _config;

        public PendingBuildExecutor(
            IPendingStore store,
            IEventBus events,
            TmbsPendingConfig config)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _events = events;
            _config = config ?? new TmbsPendingConfig();
        }

        public void Execute(in PipelineContext ctx, Tilemap targetTilemap)
        {
            if (ctx.AlternateBehaviour && !_config.pendingOnErase)
                return;

            var bounds = ctx.HasDragBounds
                ? ctx.DragBounds
                : new BoundsInt(ctx.Cell, Vector3Int.one);

            var writeMask = ctx.Decision.WriteMask ?? TMBS.Core.Validation.CellMaskPool.Rent(bounds, true);

            if (!writeMask.AnyTrue())
                return;

            if (_config.groupingMode == PendingGroupingMode.CellBlocks)
                CreateBlockPending(ctx, bounds, writeMask);
            else
                CreateIndividualPendings(ctx, writeMask);
        }

        private void CreateIndividualPendings(in PipelineContext ctx, TMBS.Core.Validation.CellMask writeMask)
        {
            for (int i = 0; i < writeMask.Length; i++)
            {
                if (!writeMask.Bits[i]) continue;

                var cell = writeMask.CellAt(i);

                if (_store.TryGetByCell(cell, out var existing, out _))
                {
                    if (!_config.replaceExistingPending)
                        continue;
                    _store.Remove(existing.Id);
                }

                var cellData = new PendingCell(
                    cell,
                    ctx.Decision.WriteTile,
                    ctx.BuildableData,
                    ResolveWorkCost(ctx, cell),
                    0,
                    ResolveResourceCost(ctx, cell),
                    0);

                var cellBounds = new BoundsInt(cell, Vector3Int.one);

                var pending = new PendingOrder(
                    ctx.InstanceId,
                    cellBounds,
                    new[] { cellData },
                    ResolveInitialState());

                int id = _store.Add(pending);

                if (_config.emitPendingEvents)
                    _events?.Publish(new PendingCreatedEvent(ctx.InstanceId, id, cellBounds));
            }
        }

        private void CreateBlockPending(in PipelineContext ctx, BoundsInt bounds, TMBS.Core.Validation.CellMask writeMask)
        {
            int count = 0;
            for (int i = 0; i < writeMask.Length; i++)
                if (writeMask.Bits[i]) count++;

            if (count == 0) return;

            for (int i = 0; i < writeMask.Length; i++)
            {
                if (!writeMask.Bits[i]) continue;

                var cell = writeMask.CellAt(i);

                if (_store.TryGetByCell(cell, out var existing, out _))
                {
                    if (!_config.replaceExistingPending)
                        return;

                    _store.Remove(existing.Id);
                }
            }

            var cells = new PendingCell[count];
            int w = 0;

            for (int i = 0; i < writeMask.Length; i++)
            {
                if (!writeMask.Bits[i]) continue;
                var cell = writeMask.CellAt(i);

                cells[w++] = new PendingCell(
                    cell,
                    ctx.Decision.WriteTile,
                    ctx.BuildableData,
                    ResolveWorkCost(ctx, cell),
                    0,
                    ResolveResourceCost(ctx, cell),
                    0);
            }

            var pending = new PendingOrder(
                ctx.InstanceId,
                bounds,
                cells,
                ResolveInitialState());

            int id = _store.Add(pending);

            if (_config.emitPendingEvents)
                _events?.Publish(new PendingCreatedEvent(ctx.InstanceId, id, bounds));
        }

        private int ResolveWorkCost(in PipelineContext ctx, Vector3Int cell)
        {
            var data = ctx.BuildableData;
            if (data == null) return _config.workCost;
            return _config.costMode == PendingCostMode.DefinitionProvider
                ? data.GetWorkCost(cell)
                : data.workCost;
        }

        private int ResolveResourceCost(in PipelineContext ctx, Vector3Int cell)
        {
            if (_config.resourcePolicy == ResourceRequirement.None) return 0;
            var data = ctx.BuildableData;
            if (data == null) return _config.resourceCost;
            return _config.costMode == PendingCostMode.DefinitionProvider
                ? data.GetResourceCost(cell)
                : data.resourceCost;
        }

        private PendingState ResolveInitialState()
        {
            switch (_config.resourcePolicy)
            {
                case ResourceRequirement.RequiredBeforeWork:
                case ResourceRequirement.AllowPartialDelivery:
                    return PendingState.WaitingForResources;
                case ResourceRequirement.None:
                default:
                    return PendingState.ReadyToBuild;
            }
        }
    }
}
