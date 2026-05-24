using TMBS.Runtime.Config;
using UnityEngine;

namespace TMBS.Core.Pending
{
    public sealed class PendingDebugBuilder
    {
        private readonly IPendingConstructionWorkApi _workApi;
        private readonly TmbsPendingDebugConfig _config;
        private int _cursor;

        public PendingDebugBuilder(IPendingConstructionWorkApi workApi, TmbsPendingDebugConfig config)
        {
            _workApi = workApi;
            _config = config;
        }

        public void Step()
        {
            if (_workApi == null || _config == null || !_config.enabled)
                return;

            var all = _workApi.GetAll();
            if (all == null || all.Count == 0)
                return;

            int processed = 0;
            int safety = 0;

            while (processed < _config.cellsPerStep && safety < all.Count)
            {
                safety++;

                if (_cursor >= all.Count)
                    _cursor = 0;

                var pending = all[_cursor++];
                if (pending == null || pending.Cells == null)
                    continue;

                if (TryProcessPending(pending))
                    processed++;
            }
        }

        private bool TryProcessPending(PendingConstruction pending)
        {
            var cells = pending.Cells;

            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];

                if (_config.autoDeliverResources && !cell.IsReadyForWork)
                {
                    _workApi.TryDeliverResources(cell.Cell, cell.ResourceRequired);
                    return true;
                }

                if (!cell.IsReadyForWork || cell.IsWorkComplete)
                    continue;

                _workApi.TryAddWork(cell.Cell, _config.workPerStep);

                if (_workApi.TryGetCellStatus(cell.Cell, out var updated) && updated.IsWorkComplete)
                    _workApi.TryCompleteCell(cell.Cell);

                return true;
            }

            return false;
        }
    }
}