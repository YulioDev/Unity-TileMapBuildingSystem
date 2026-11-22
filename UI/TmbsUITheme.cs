using System;
using System.Collections.Generic;
using TMBS.Runtime.Config;
using UnityEngine;

namespace TMBS.UI
{
    [Serializable]
    public struct TmbsCategoryColorEntry
    {
        public string category;
        public Color color;
    }

    [Serializable]
    public sealed class TmbsUIThemeSettings
    {
        [Header("Surfaces & Panels")]
        public Color panelBackgroundColor = new Color(0.11f, 0.12f, 0.14f, 0.94f);
        public Color panelBorderColor = new Color(1f, 1f, 1f, 0.10f);
        public Color headerAccentColor = new Color(0.38f, 0.65f, 0.95f, 1f);
        public Color pendingPanelColor = new Color(0.20f, 0.16f, 0.07f, 0.96f);
        public Color pendingAccentColor = new Color(0.96f, 0.65f, 0.14f, 1f);

        [Header("Catalog Slots & Tabs")]
        public Color slotNormalColor = new Color(0.16f, 0.18f, 0.21f, 0.96f);
        public Color slotSelectedColor = new Color(0.23f, 0.28f, 0.36f, 1.00f);
        public Color selectionBorderColor = new Color(0.42f, 0.72f, 1.00f, 1.00f);
        public Color slotNormalLabelColor = new Color(0.92f, 0.94f, 0.96f, 1.00f);
        public Color slotSelectedLabelColor = new Color(1.00f, 0.98f, 0.90f, 1.00f);
        public Color tabActiveColor = new Color(0.24f, 0.29f, 0.37f, 1.00f);
        public Color tabInactiveColor = new Color(0.14f, 0.16f, 0.19f, 0.90f);
        public Color badgePillColor = new Color(0.07f, 0.08f, 0.10f, 0.84f);
        public Color badgeTextColor = new Color(0.93f, 0.86f, 0.60f, 1.00f);

        [Header("Toolbar & Toasts")]
        public Color neutralButtonColor = new Color(0.18f, 0.20f, 0.24f, 0.96f);
        public Color eraseIdleColor = new Color(0.25f, 0.18f, 0.17f, 0.96f);
        public Color eraseActiveColor = new Color(0.76f, 0.24f, 0.24f, 1.00f);
        public Color clearButtonColor = new Color(0.24f, 0.17f, 0.18f, 0.96f);
        public Color infoToastColor = new Color(0.13f, 0.15f, 0.18f, 0.96f);
        public Color warningToastColor = new Color(0.52f, 0.18f, 0.19f, 0.96f);
        public Color warningBorderColor = new Color(0.95f, 0.35f, 0.35f, 1.00f);

        [Header("Generic Cost & Badge Formatting")]
        [Tooltip("Prefix shown before resource cost (e.g. '', '$', 'Cost '). Empty by default for genre neutrality.")]
        public string resourceCostPrefix = "";
        public string workCostPrefix = "W:";
        public bool showResourceCostOnSlots = true;
        public bool showWorkCostOnSlots = false;
        public bool showFootprintOnSlots = true;
        public bool hideZeroCostInHud = true;

        [Header("Category Colors (Auto-Generated if not in list)")]
        public List<TmbsCategoryColorEntry> categoryOverrides = new List<TmbsCategoryColorEntry>();

        public Color ResolveCategoryColor(string category)
        {
            string norm = string.IsNullOrWhiteSpace(category)
                ? "general"
                : category.Trim().ToLowerInvariant();

            if (categoryOverrides != null)
            {
                for (int i = 0; i < categoryOverrides.Count; i++)
                {
                    var entry = categoryOverrides[i];
                    if (!string.IsNullOrWhiteSpace(entry.category) &&
                        string.Equals(entry.category.Trim(), norm, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.color;
                    }
                }
            }

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < norm.Length; i++)
                {
                    hash ^= norm[i];
                    hash *= 16777619u;
                }

                float hue = ((hash % 360u) / 360f + 0.61803398875f) % 1f;
                return Color.HSVToRGB(hue, 0.48f, 0.84f);
            }
        }

        public string FormatSlotMetaBadge(TmbsBuildableData data)
        {
            if (data == null)
                return string.Empty;

            var parts = new List<string>(2);

            if (showFootprintOnSlots && data.isBlueprint)
            {
                Vector2Int sz = data.GetSize();
                if (sz.x > 1 || sz.y > 1)
                    parts.Add($"{sz.x}×{sz.y}");
            }

            if (showResourceCostOnSlots && data.resourceCost > 0)
            {
                string prefix = resourceCostPrefix ?? string.Empty;
                parts.Add($"{prefix}{data.resourceCost}");
            }
            else if (showWorkCostOnSlots && data.workCost > 1)
            {
                string wPrefix = workCostPrefix ?? "W:";
                parts.Add($"{wPrefix}{data.workCost}");
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
        }
    }
}
