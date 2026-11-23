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
        private IPendingWorkApi _workApi;

        public PendingBuildExecutor(
            IPendingStore store,
            IEventBus events,
            TmbsPendingConfig config,
            IUndoRedoHistory history = null,
            IPendingWorkApi workApi = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _events = events;
            _config = config ?? new TmbsPendingConfig();
            _history = history;
            _workApi = workApi;
        }

        public void InjectWorkApi(IPendingWorkApi workApi) => _workApi = workApi;

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

            List<int> cancelOrderIds = null;

            if (ctx.AlternateBehaviour)
            {
                var copy = TMBS.Core.Validation.CellMaskPool.Rent(writeMask.Bounds, false);
                Array.Copy(writeMask.Bits, copy.Bits, writeMask.Length);
                writeMask = copy;

                cancelOrderIds = new List<int>();
                for (int i = 0; i < writeMask.Length; i++)
                {
                    if (!writeMask.Bits[i]) continue;

                    var cell = writeMask.CellAt(i);
                    if (_store.TryGetByCell(cell, out var existingPending, out int idx) &&
                        !existingPending.Cells[idx].IsCompleted)
                    {
                        if (!cancelOrderIds.Contains(existingPending.Id))
                            cancelOrderIds.Add(existingPending.Id);

                        writeMask.Bits[i] = false;
                    }
                    else if (targetTilemap == null || !targetTilemap.HasTile(cell))
                    {
                        writeMask.Bits[i] = false;
                    }
                }
            }

            bool isBlueprint = !ctx.AlternateBehaviour && ctx.BuildableData != null && ctx.BuildableData.isBlueprint;
            List<(BoundsInt bounds, PendingCell[] cells, PendingState initialState)> orderDefs = null;

            if (writeMask.AnyTrue())
            {
                if (isBlueprint || _config.groupingMode == PendingGroupingMode.CellBlocks)
                    orderDefs = BuildBlockPendingOrders(ctx, bounds, writeMask);
                else
                    orderDefs = BuildIndividualPendingOrders(ctx, writeMask);
            }

            bool hasNewOrders = orderDefs != null && orderDefs.Count > 0;
            bool hasCancellations = cancelOrderIds != null && cancelOrderIds.Count > 0;
            if (!hasNewOrders && !hasCancellations)
                return;

            var command = new PendingOrderCommand(
                _store,
                _events,
                ctx.InstanceId,
                orderDefs,
                _config.emitPendingEvents,
                _config.replaceExistingPending,
                cancelOrderIds,
                _workApi);

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

            for (int i = 0; i < writeMask.Length; i++)
            {
                if (!writeMask.Bits[i]) continue;
                var cell = writeMask.CellAt(i);
                var cellBounds = new BoundsInt(cell, Vector3Int.one);
                var cellData = CreatePendingCell(in ctx, cellBounds, cell, out int resCost);

                list.Add((cellBounds, new[] { cellData }, ResolveInitialState(resCost)));
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
            int totalResCost = 0;

            for (int i = 0; i < writeMask.Length; i++)
            {
                if (!writeMask.Bits[i]) continue;
                var cell = writeMask.CellAt(i);
                cells[w++] = CreatePendingCell(in ctx, bounds, cell, out int resCost);
                totalResCost += resCost;
            }

            return new List<(BoundsInt bounds, PendingCell[] cells, PendingState initialState)>
            {
                (bounds, cells, ResolveInitialState(totalResCost))
            };
        }

        private PendingCell CreatePendingCell(in PipelineContext ctx, BoundsInt bounds, Vector3Int cell, out int resourceCost)
        {
            resourceCost = ResolveResourceCost(ctx, cell);
            TileBase tile = !ctx.AlternateBehaviour && ctx.BuildableData != null && ctx.BuildableData.isBlueprint
                ? (ctx.BuildableData.GetTileAtLocalOffset(cell.x - bounds.xMin, cell.y - bounds.yMin) ?? ctx.Decision.WriteTile)
                : ctx.Decision.WriteTile;

            return new PendingCell(
                cell,
                tile,
                ctx.BuildableData,
                ResolveWorkCost(ctx, cell),
                0,
                resourceCost,
                0,
                false,
                ctx.Decision.TransformMatrix);
        }

        private int ResolveWorkCost(in PipelineContext ctx, Vector3Int cell)
        {
            if (_config.costMode == PendingCostMode.FixedPerCell)
                return Mathf.Max(1, _config.workCost);

            var data = ctx.BuildableData;
            return data != null
                ? Mathf.Max(1, data.GetWorkCost(cell))
                : Mathf.Max(1, _config.workCost);
        }

        private int ResolveResourceCost(in PipelineContext ctx, Vector3Int cell)
        {
            if (ctx.AlternateBehaviour || _config.resourcePolicy == ResourceRequirement.None) return 0;
            if (_config.costMode == PendingCostMode.FixedPerCell)
                return Mathf.Max(0, _config.resourceCost);

            var data = ctx.BuildableData;
            return data != null
                ? Mathf.Max(0, data.GetResourceCost(cell))
                : Mathf.Max(0, _config.resourceCost);
        }

        private PendingState ResolveInitialState(int resourceCost = 1)
        {
            if (resourceCost <= 0) return PendingState.ReadyToBuild;
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
