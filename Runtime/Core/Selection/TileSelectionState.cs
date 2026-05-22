using UnityEngine.Tilemaps;

namespace TMBS.Core.Selection
{
    public sealed class TileSelectionState
    {
        public TileBase CurrentTile { get; private set; }
        public bool HasTile => CurrentTile != null;

        public void UpdateFromSelection(BuildSelectionData data)
        {
            CurrentTile = data.BuildTile;
        }
    }
}