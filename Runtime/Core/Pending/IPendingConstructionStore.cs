using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Pending
{
    public interface IPendingConstructionStore
    {
        int Add(PendingConstruction pending);
        bool TryGet(int id, out PendingConstruction pending);
        bool TryGetByCell(Vector3Int cell, out PendingConstruction pending, out int cellIndex);
        bool Remove(int id);
        IReadOnlyList<PendingConstruction> All { get; }
    }
}