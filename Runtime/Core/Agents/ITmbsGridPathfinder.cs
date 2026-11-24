using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Agents
{
    public interface ITmbsGridPathfinder
    {
        bool TryFindPath(
            Vector3Int start,
            Vector3Int goal,
            ITmbsGridWalkabilityProvider walkability,
            List<Vector3Int> pathBuffer,
            bool allowDiagonal = false,
            int maxExpandedNodes = 2048);

        bool TryFindPathToAnyGoal(
            Vector3Int start,
            IReadOnlyList<Vector3Int> goals,
            ITmbsGridWalkabilityProvider walkability,
            List<Vector3Int> pathBuffer,
            out Vector3Int reachedGoal,
            bool allowDiagonal = false,
            int maxExpandedNodes = 2048);
    }
}
