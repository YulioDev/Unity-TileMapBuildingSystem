using UnityEngine;

namespace TMBS.Core.Validation
{
    public interface ICellOccupancySource
    {
        bool HasTile(Vector3Int cell);
    }

    public interface IOccupancySourceInjectable
    {
        void InjectOccupancySource(ICellOccupancySource occupancySource);
    }
}
