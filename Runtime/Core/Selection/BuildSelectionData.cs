using UnityEngine.Tilemaps;

namespace TMBS.Core.Selection
{
    public readonly struct BuildSelectionData
    {
        public readonly TileBase BuildTile;
        public readonly TileBase PreviewValidTile;
        public readonly TileBase PreviewInvalidTile;

        public bool HasBuildTile => BuildTile != null;

        public TileBase ResolvedPreviewValid   => PreviewValidTile   ?? BuildTile;
        public TileBase ResolvedPreviewInvalid => PreviewInvalidTile ?? BuildTile;

        public BuildSelectionData(TileBase build, TileBase previewValid, TileBase previewInvalid)
        {
            BuildTile          = build;
            PreviewValidTile   = previewValid;
            PreviewInvalidTile = previewInvalid;
        }
    }
}