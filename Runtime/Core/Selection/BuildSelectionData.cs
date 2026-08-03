using UnityEngine.Tilemaps;

namespace TMBS.Core.Selection
{
    public readonly struct BuildSelectionData
    {
        public readonly TileBase BuildTile;

        public bool HasBuildTile => BuildTile != null;

        public BuildSelectionData(TileBase build)
        {
            BuildTile = build;
        }
    }
}