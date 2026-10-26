using System;
using System.Collections.Generic;
using TMBS.Core.Events;
using TMBS.Core.Execution;
using TMBS.Core.History;
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
        private readonly IUndoRedoHistory _history;

        public PendingBuildExecutor(
            IPendingStore store,
            IEventBus events,
            TmbsPendingConfig config,
            IUndoRedoHistory history = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _events = events;
            _config = config ?? new TmbsPendingConfig();
            _history = history;
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

            bool isBlueprint = ctx.BuildableData != null && ctx.BuildableData.isBlueprint;
            List<(BoundsInt bounds, PendingCell[] cells, PendingState initialState)> orderDefs;

            if (isBlueprint || _config.groupingMode == PendingGroupingMode.CellBlocks)
                orderDefs = BuildBlockPendingOrders(ctx, bounds, writeMask);
            else
                orderDefs = BuildIndividualPendingOrders(ctx, writeMask);

            if (orderDefs == null || orderDefs.Count == 0)
                return;

            var command = new PendingOrderCommand(
                _store,
                _events,
                ctx.InstanceId,
                orderDefs,
                _config.emitPendingEvents,
                _config.replaceExistingPending);

            if (_history != null)
            {
                _history.Push(command);
            }
            else
            {
                command.Execute();
            }
        }

        private List<(BoundsInt bounds, PendingCell[] cells, PendingState initialState)> BuildIndividualPendingOrders(
            in PipelineContext ctx, TMBS.Core.Validation.CellMask writeMask)
        {
            var list = new List<(BoundsInt bounds, PendingCell[] cells, PendingState initialState)>();
            var initialState = ResolveInitialState();

            for (int i = 0; i < writeMask.Length; i++)
            {
                if (!writeMask.Bits[i]) continue;
                var cell = writeMask.CellAt(i);

                var cellData = new PendingCell(
                    cell,
                    ctx.Decision.WriteTile,
                    ctx.BuildableData,
                    ResolveWorkCost(ctx, cell),
                    0,
                    ResolveResourceCost(ctx, cell),
                    0,
                    false,
                    ctx.Decision.TransformMatrix);

                var cellBounds = new BoundsInt(cell, Vector3Int.one);
                list.Add((cellBounds, new[] { cellData }, initialState));
            }

            return list;
        }

        private List<(BoundsInt bounds, PendingCell[] cells, PendingState initialState)> BuildBlockPendingOrders(
            in PipelineContext ctx, BoundsInt bounds, TMBS.Core.Validation.CellMask writeMask)
        {
            int count = 0;
            for (int i = 0; i < writeMask.Length; i++)
                if (writeMask.Bits[i]) count++;

            if (count == 0) return null;

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
                    0,
                    false,
                    ctx.Decision.TransformMatrix);
            }

            return new List<(BoundsInt bounds, PendingCell[] cells, PendingState initialState)>
            {
                (bounds, cells, ResolveInitialState())
            };
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
