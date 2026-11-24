using UnityEngine;

namespace TMBS.Core.Agents
{
    public interface ITmbsGridWalkabilityProvider
    {
        bool IsWalkable(Vector3Int cell);
    }
}
