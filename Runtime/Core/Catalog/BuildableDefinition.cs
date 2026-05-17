using UnityEngine.Tilemaps;

namespace TMBS.Core.Catalog
{
    public sealed class BuildableDefinition
    {
        public readonly int Id;
        public readonly string LogicalName;
        public readonly TileBase VisualTile;

        public BuildableDefinition(int id, string logicalName, TileBase visualTile)
        {
            Id = id;
            LogicalName = logicalName;
            VisualTile = visualTile;
        }
    }
}

