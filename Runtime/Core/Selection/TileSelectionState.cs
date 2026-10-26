using UnityEngine.Tilemaps;
using TMBS.Runtime.Config;

namespace TMBS.Core.Selection
{
    public sealed class TileSelectionState
    {
        public TileBase CurrentTile { get; private set; }
        public TmbsBuildableData CurrentData { get; private set; }
        public bool HasTile => CurrentTile != null;
        public PlacementTransform Transform { get; set; }

        public void Rotate() => Transform = new PlacementTransform((Transform.Rotation + 1) % 4, Transform.Mirrored);
        public void Mirror() => Transform = new PlacementTransform(Transform.Rotation, !Transform.Mirrored);

        public void UpdateFromSelection(BuildSelectionData data)
        {
            CurrentTile = data.BuildTile;
            CurrentData = data.BuildableData;
            Transform = default;
        }
    }
}