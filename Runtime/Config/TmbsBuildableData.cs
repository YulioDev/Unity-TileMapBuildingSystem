using TMBS.Core.Pending;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Runtime.Config
{
    [CreateAssetMenu(menuName = "TMBS/Buildable Data", fileName = "NewBuildable")]
    public class TmbsBuildableData : ScriptableObject, IPendingDefinition
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this buildable. Used for lookups and save systems.")]
        public string id;

        [Tooltip("Display name shown in UI and editor.")]
        public string displayName = "New Buildable";

        [Tooltip("Category for grouping in the editor gallery (e.g. 'Walls', 'Floors', 'Decorations').")]
        public string category = "General";

        [Header("Visual")]
        [Tooltip("The tile asset to place on the tilemap.")]
        public TileBase tile;

        [Tooltip("Optional sprite used for UI thumbnails and editor preview. If null, the system will attempt to extract the sprite from the tile.")]
        public Sprite previewSprite;

        [Header("Construction Costs")]
        [Min(1)]
        [Tooltip("Work units required to complete construction of one cell.")]
        public int workCost = 1;

        [Min(0)]
        [Tooltip("Resource units required for construction of one cell.")]
        public int resourceCost = 0;

        [Header("Blueprint")]
        [Tooltip("If true, this buildable represents a multi-tile construction with a custom footprint.")]
        public bool isBlueprint = false;

        [Tooltip("Size in cells. Only used when isBlueprint is true.")]
        public Vector2Int footprintSize = Vector2Int.one;

        public Vector2Int GetSize() =>
            isBlueprint ? new Vector2Int(Mathf.Max(1, footprintSize.x), Mathf.Max(1, footprintSize.y))
                        : Vector2Int.one;

        public BoundsInt GetFootprint(Vector3Int anchor)
        {
            var size = GetSize();
            return new BoundsInt(anchor, new Vector3Int(size.x, size.y, 1));
        }

        public virtual int GetWorkCost(Vector3Int cell) => workCost;
        public virtual int GetResourceCost(Vector3Int cell) => resourceCost;
    }
}
