using System;
using System.Collections.Generic;
using TMBS.Core.Pending;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Runtime.Config
{
    public enum TmbsPlacementRole
    {
        Structure = 0,
        Floor = 1,
        Wall = 2,
        Decoration = 3,
        Utility = 4,
        Resource = 5,
        TrapHazard = 6,
        TriggerZone = 7,
        Custom = 99
    }

    public enum TmbsBuildableSortMode
    {
        CatalogOrder = 0,
        Name = 1,
        Category = 2,
        Role = 3,
        Tier = 4,
        ResourceCost = 5,
        WorkCost = 6,
        FootprintArea = 7
    }

    [CreateAssetMenu(menuName = "TMBS/Buildable Data", fileName = "NewBuildable")]
    public class TmbsBuildableData : ScriptableObject, IPendingDefinition
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this buildable. Used for lookups and save systems.")]
        public string id;

        [Tooltip("Display name shown in UI and editor.")]
        public string displayName = "New Buildable";

        [TextArea(2, 4)]
        [Tooltip("Optional short description shown in tooltips and editor inspector.")]
        public string description;

        [Header("Classification")]
        [Tooltip("Category for grouping in the editor gallery (e.g. 'Walls', 'Floors', 'Decorations').")]
        public string category = "General";

        [Tooltip("Optional secondary grouping within a category (e.g. 'Wood', 'Stone', 'Power').")]
        public string subcategory;

        [Tooltip("Broad gameplay/placement role to classify buildables across genres.")]
        public TmbsPlacementRole placementRole = TmbsPlacementRole.Structure;

        [Min(0)]
        [Tooltip("Progression tier or unlock level (0 = starter / untiered).")]
        public int tier = 0;

        [Tooltip("Manual catalog ordering priority. Lower values appear earlier in CatalogOrder sorting.")]
        public int sortOrder = 0;

        [Tooltip("Flexible multi-genre tags (e.g. 'structural', 'outdoor', 'interactive').")]
        public List<string> tags = new List<string>();

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

        [Header("Gameplay & Placement")]
        [Min(0)]
        [Tooltip("Optional hit points / durability (0 = indestructible or unused).")]
        public int maxHealth = 0;

        [Tooltip("Whether this buildable supports rotation during placement.")]
        public bool canRotate = true;

        [Tooltip("Whether this buildable supports mirroring/flipping during placement.")]
        public bool canMirror = false;

        [Tooltip("Whether this buildable blocks character or entity movement by default.")]
        public bool blocksMovement = true;

        public string GetEffectiveId() =>
            !string.IsNullOrWhiteSpace(id) ? id.Trim() : name;

        public string GetEffectiveDisplayName() =>
            !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : name;

        public string GetEffectiveCategory() =>
            !string.IsNullOrWhiteSpace(category) ? category.Trim() : "General";

        public string GetEffectiveSubcategory() =>
            string.IsNullOrWhiteSpace(subcategory) ? string.Empty : subcategory.Trim();

        public Vector2Int GetSize() =>
            isBlueprint ? new Vector2Int(Mathf.Max(1, footprintSize.x), Mathf.Max(1, footprintSize.y))
                        : Vector2Int.one;

        public int GetFootprintArea()
        {
            var size = GetSize();
            return size.x * size.y;
        }

        public BoundsInt GetFootprint(Vector3Int anchor)
        {
            var size = GetSize();
            return new BoundsInt(anchor, new Vector3Int(size.x, size.y, 1));
        }

        public virtual int GetWorkCost(Vector3Int cell) => workCost;
        public virtual int GetResourceCost(Vector3Int cell) => resourceCost;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null || tags.Count == 0)
                return false;

            string target = tag.Trim();
            for (int i = 0; i < tags.Count; i++)
            {
                string current = tags[i];
                if (!string.IsNullOrWhiteSpace(current) &&
                    string.Equals(current.Trim(), target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool MatchesSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string needle = query.Trim();
            if (ContainsOrdinalIgnoreCase(GetEffectiveDisplayName(), needle) ||
                ContainsOrdinalIgnoreCase(GetEffectiveId(), needle) ||
                ContainsOrdinalIgnoreCase(GetEffectiveCategory(), needle) ||
                ContainsOrdinalIgnoreCase(GetEffectiveSubcategory(), needle) ||
                ContainsOrdinalIgnoreCase(description, needle) ||
                ContainsOrdinalIgnoreCase(placementRole.ToString(), needle) ||
                ContainsOrdinalIgnoreCase(name, needle))
            {
                return true;
            }

            if (tags != null)
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    if (ContainsOrdinalIgnoreCase(tags[i], needle))
                        return true;
                }
            }

            return false;
        }

        public static int Compare(
            TmbsBuildableData a,
            TmbsBuildableData b,
            TmbsBuildableSortMode mode,
            bool descending = false)
        {
            if (ReferenceEquals(a, b))
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            int cmp;
            switch (mode)
            {
                case TmbsBuildableSortMode.Name:
                    cmp = CompareByName(a, b);
                    break;
                case TmbsBuildableSortMode.Category:
                    cmp = CompareByCategory(a, b);
                    if (cmp == 0) cmp = a.sortOrder.CompareTo(b.sortOrder);
                    if (cmp == 0) cmp = CompareByName(a, b);
                    break;
                case TmbsBuildableSortMode.Role:
                    cmp = ((int)a.placementRole).CompareTo((int)b.placementRole);
                    if (cmp == 0) cmp = a.sortOrder.CompareTo(b.sortOrder);
                    if (cmp == 0) cmp = CompareByName(a, b);
                    break;
                case TmbsBuildableSortMode.Tier:
                    cmp = a.tier.CompareTo(b.tier);
                    if (cmp == 0) cmp = a.sortOrder.CompareTo(b.sortOrder);
                    if (cmp == 0) cmp = CompareByName(a, b);
                    break;
                case TmbsBuildableSortMode.ResourceCost:
                    cmp = a.resourceCost.CompareTo(b.resourceCost);
                    if (cmp == 0) cmp = a.workCost.CompareTo(b.workCost);
                    if (cmp == 0) cmp = CompareByName(a, b);
                    break;
                case TmbsBuildableSortMode.WorkCost:
                    cmp = a.workCost.CompareTo(b.workCost);
                    if (cmp == 0) cmp = a.resourceCost.CompareTo(b.resourceCost);
                    if (cmp == 0) cmp = CompareByName(a, b);
                    break;
                case TmbsBuildableSortMode.FootprintArea:
                    cmp = a.GetFootprintArea().CompareTo(b.GetFootprintArea());
                    if (cmp == 0) cmp = CompareByName(a, b);
                    break;
                case TmbsBuildableSortMode.CatalogOrder:
                default:
                    cmp = a.sortOrder.CompareTo(b.sortOrder);
                    if (cmp == 0) cmp = CompareByCategory(a, b);
                    if (cmp == 0) cmp = CompareByName(a, b);
                    break;
            }

            return descending ? -cmp : cmp;
        }

        private static int CompareByCategory(TmbsBuildableData a, TmbsBuildableData b)
        {
            int cmp = string.Compare(a.GetEffectiveCategory(), b.GetEffectiveCategory(), StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
                return cmp;

            return string.Compare(a.GetEffectiveSubcategory(), b.GetEffectiveSubcategory(), StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareByName(TmbsBuildableData a, TmbsBuildableData b)
        {
            int cmp = string.Compare(a.GetEffectiveDisplayName(), b.GetEffectiveDisplayName(), StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
                return cmp;

            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsOrdinalIgnoreCase(string source, string needle)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
