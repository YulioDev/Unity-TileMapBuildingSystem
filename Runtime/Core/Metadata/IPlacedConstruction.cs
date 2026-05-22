using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Metadata
{
    public interface IPlacedConstruction
    {
        TileBase PlacedTile { get; }
        Vector3Int Cell { get; }
        BuildState State { get; }
    }
}

