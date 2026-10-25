using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Pending
{
    public interface IPendingStore
    {
        int Add(PendingOrder pending);
        bool TryGet(int id, out PendingOrder pending);
        bool TryGetByCell(Vector3Int cell, out PendingOrder pending, out int cellIndex);
        bool Remove(int id);
        IReadOnlyList<PendingOrder> All { get; }
    }
}
