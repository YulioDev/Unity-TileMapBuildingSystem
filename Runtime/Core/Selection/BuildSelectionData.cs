using UnityEngine.Tilemaps;
using TMBS.Runtime.Config;

namespace TMBS.Core.Selection
{
    public readonly struct BuildSelectionData
    {
        public readonly TileBase BuildTile;
        public readonly TmbsBuildableData BuildableData;

        public bool HasBuildTile => BuildTile != null;

        public BuildSelectionData(TileBase build, TmbsBuildableData data = null)
        {
            BuildTile = build;
            BuildableData = data;
        }
    }
}