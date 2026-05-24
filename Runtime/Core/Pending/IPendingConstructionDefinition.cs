using UnityEngine;

namespace TMBS.Core.Pending
{
    public interface IPendingConstructionDefinition
    {
        BoundsInt GetFootprint(Vector3Int anchor);
        int GetWorkCost(Vector3Int cell);
        int GetResourceCost(Vector3Int cell);
    }
}