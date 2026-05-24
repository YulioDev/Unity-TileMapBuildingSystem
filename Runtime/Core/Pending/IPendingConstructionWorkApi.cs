using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Pending
{
    public interface IPendingConstructionWorkApi
    {
        bool TryDeliverResources(Vector3Int cell, int amount);
        bool TryAddWork(Vector3Int cell, int amount);
        bool TryCompleteCell(Vector3Int cell);
        bool TryCancel(int pendingId);
        bool TryGetCellStatus(Vector3Int cell, out PendingConstructionCell status);
        IReadOnlyList<PendingConstruction> GetAll();
    }
}