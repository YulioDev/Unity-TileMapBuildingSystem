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
        private readonly IPendingConstructionStore _store;
        private readonly IEventBus _events;
        private readonly TmbsPendingConstructionConfig _config;

        public PendingBuildExecutor(
            IPendingConstructionStore store,
            IEventBus events,
            TmbsPendingConstructionConfig config)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _events = events;
            _config = config ?? new TmbsPendingConstructionConfig();
        }

        public void Execute(in PipelineContext ctx, Tilemap targetTilemap)
        {
            if (ctx.AlternateBehaviour && !_config.createPendingForAlternateBehaviour)
                return;

            var bounds = ctx.HasDragBounds
                ? ctx.DragBounds
                : new BoundsInt(ctx.Cell, Vector3Int.one);

            var writeMask = ctx.Decision.WriteMask ?? TMBS.Core.Validation.CellMask.AllTrue(bounds);

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

                var cellData = new PendingConstructionCell(
                    cell,
                    ctx.Decision.WriteTile,
                    _config.fixedWorkPerCell,
                    0,
                    _config.fixedResourceCostPerCell,
                    0);

                var cellBounds = new BoundsInt(cell, Vector3Int.one);

                var pending = new PendingConstruction(
                    ctx.InstanceId,
                    cellBounds,
                    new[] { cellData },
                    ResolveInitialState());

                int id = _store.Add(pending);

                if (_config.emitPendingEvents)
                    _events?.Publish(new PendingConstructionCreatedEvent(ctx.InstanceId, id, cellBounds));
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

            var cells = new PendingConstructionCell[count];
            int w = 0;

            for (int i = 0; i < writeMask.Length; i++)
            {
                if (!writeMask.Bits[i]) continue;
                var cell = writeMask.CellAt(i);

                cells[w++] = new PendingConstructionCell(
                    cell,
                    ctx.Decision.WriteTile,
                    _config.fixedWorkPerCell,
                    0,
                    _config.fixedResourceCostPerCell,
                    0);
            }

            var pending = new PendingConstruction(
                ctx.InstanceId,
                bounds,
                cells,
                ResolveInitialState());

            int id = _store.Add(pending);

            if (_config.emitPendingEvents)
                _events?.Publish(new PendingConstructionCreatedEvent(ctx.InstanceId, id, bounds));
        }

        private PendingConstructionState ResolveInitialState() =>
            _config.resourcePolicy == PendingResourcePolicy.RequiredBeforeWork
                ? PendingConstructionState.WaitingForResources
                : PendingConstructionState.ReadyToBuild;
    }
}