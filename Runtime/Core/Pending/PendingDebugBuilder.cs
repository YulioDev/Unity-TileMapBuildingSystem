using System.Collections.Generic;
using TMBS.Runtime.Config;
using UnityEngine;

namespace TMBS.Core.Pending
{
    public sealed class PendingDebugBuilder
    {
        private readonly IPendingWorkApi _workApi;
        private readonly TmbsPendingDebugConfig _config;
        private readonly ResourceRequirement _policy;
        private int _cursor;

        public PendingDebugBuilder(
            IPendingWorkApi workApi, 
            TmbsPendingDebugConfig config,
            ResourceRequirement policy = ResourceRequirement.None)
        {
            _workApi = workApi;
            _config = config;
            _policy = policy;
        }

        public void Step()
        {
            if (_workApi == null || _config == null || !_config.enabled)
                return;

            var all = _workApi.GetAll();
            if (all == null || all.Count == 0)
                return;

            IReadOnlyList<PendingOrder> ordered = all;
            if (_config.buildOrder == TMBS.Runtime.Config.PendingDebugBuildOrder.ByCellPosition)
            {
                var sorted = new List<PendingOrder>(all);
                sorted.Sort((a, b) =>
                {
                    int cmp = a.Bounds.position.x.CompareTo(b.Bounds.position.x);
                    return cmp != 0 ? cmp : a.Bounds.position.y.CompareTo(b.Bounds.position.y);
                });
                ordered = sorted;
            }

            int processed = 0;
            int safety = 0;

            while (processed < _config.cellsPerStep && safety < ordered.Count)
            {
                safety++;

                if (_cursor >= ordered.Count)
                    _cursor = 0;

                var pending = ordered[_cursor++];
                if (pending == null || pending.Cells == null)
                    continue;

                if (TryProcessPending(pending))
                    processed++;
            }
        }

        private bool TryProcessPending(PendingOrder pending)
        {
            var cells = pending.Cells;

            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];

                if (cell.IsCompleted)
                    continue;

                if (cell.IsWorkComplete)
                {
                    _workApi.TryCompleteCell(cell.Cell);
                    return true;
                }

                int allowedWork = cell.WorkAllowed(_policy);
                if (cell.WorkDone < allowedWork)
                {
                    int workToAdd = Mathf.Min(allowedWork - cell.WorkDone, _config.workPerStep);
                    _workApi.TryAddWork(cell.Cell, workToAdd);

                    if (_workApi.TryGetCellStatus(cell.Cell, out var updated) && updated.IsWorkComplete)
                        _workApi.TryCompleteCell(cell.Cell);

                    return true;
                }

                if (_config.autoDeliverResources && cell.ResourceDelivered < cell.ResourceRequired)
                {
                    int needed = cell.ResourceRequired - cell.ResourceDelivered;
                    _workApi.TryDeliverResources(cell.Cell, needed);
                    return true;
                }
            }

            return false;
        }
    }
}
