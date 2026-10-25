using UnityEngine;

namespace TMBS.Core.Validation
{
    public interface ICellOccupancySource
    {
        bool HasTile(Vector3Int cell);
    }

    public interface IOccupancyConsumer
    {
        void InjectOccupancySource(ICellOccupancySource occupancySource);
    }
}
