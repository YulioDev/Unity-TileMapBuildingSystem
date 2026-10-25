using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Pending
{
    public interface IPendingWorkApi
    {
        bool TryDeliverResources(Vector3Int cell, int amount);
        bool TryAddWork(Vector3Int cell, int amount);
        bool TryCompleteCell(Vector3Int cell);
        bool TryCancel(int pendingId);
        bool TryGetCellStatus(Vector3Int cell, out PendingCell status);
        IReadOnlyList<PendingOrder> GetAll();
    }
}
