using System;
using UnityEngine;
using UnityEngine.UI;
using TMBS.Runtime.Config;
using UnityEngine.Tilemaps;

namespace TMBS.UI
{
    /// <summary>
    /// Represents a single selectable buildable or tile slot in the TMBS UI palette.
    /// </summary>
    [DisallowMultipleComponent]
    public class TmbsPaletteSlot : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Text labelText;
        [SerializeField] private Image highlightBorder;
        [SerializeField] private Button slotButton;
        [SerializeField] private Text costBadgeText;
        [SerializeField] private Image categoryColorBar;

        [Header("State")]
        [SerializeField] private Color selectedColor = new Color(0.2f, 0.8f, 1f, 1f);
        [SerializeField] private Color normalBorderColor = new Color(0.25f, 0.25f, 0.3f, 0.6f);

        public TmbsBuildableData BuildableData { get; private set; }
        public TileBase DirectTile { get; private set; }
        public bool IsSelected { get; private set; }
        public string ItemName { get; private set; }
        public string Category { get; private set; }

        private Action<TmbsPaletteSlot> _onSelectedCallback;

        private void Awake()
        {
            if (slotButton == null)
                slotButton = GetComponent<Button>();

            if (slotButton != null)
                slotButton.onClick.AddListener(OnClick);
        }

        /// <summary>
        /// Configures this slot using a TmbsBuildableData ScriptableObject.
        /// </summary>
        public void Setup(TmbsBuildableData data, Action<TmbsPaletteSlot> onSelected)
        {
            BuildableData = data;
            DirectTile = data != null ? data.tile : null;
            _onSelectedCallback = onSelected;

            if (data != null)
            {
                ItemName = !string.IsNullOrEmpty(data.displayName) ? data.displayName : data.name;
                Category = !string.IsNullOrEmpty(data.category) ? data.category : "General";

                // Resolve icon: prefer previewSprite, fallback to RuleTile / Tile / TileData
                Sprite icon = ResolveSprite(data, DirectTile);

                SetIcon(icon);
                SetLabel(ItemName);

                if (costBadgeText != null)
                {
                    if (data.resourceCost > 0)
                        costBadgeText.text = $"${data.resourceCost}";
                    else if (data.workCost > 1)
                        costBadgeText.text = $"W:{data.workCost}";
                    else
                        costBadgeText.text = "";
                }

                if (categoryColorBar != null)
                {
                    categoryColorBar.color = GetCategoryColor(Category);
                }
            }
            else
            {
                ItemName = "None";
                Category = "None";
                SetIcon(null);
                SetLabel("None");
                if (costBadgeText != null) costBadgeText.text = "";
            }

            SetSelected(false);
        }

        /// <summary>
        /// Configures this slot directly from a TileBase (e.g. RuleTile or Tile).
        /// </summary>
        public void Setup(TileBase tile, string displayName, Sprite customIcon, string category, Action<TmbsPaletteSlot> onSelected)
        {
            BuildableData = null;
            DirectTile = tile;
            _onSelectedCallback = onSelected;
            ItemName = !string.IsNullOrEmpty(displayName) ? displayName : (tile != null ? tile.name : "Tile");
            Category = !string.IsNullOrEmpty(category) ? category : "Tiles";

            Sprite icon = customIcon != null ? customIcon : ResolveSprite(null, tile);

            SetIcon(icon);
            SetLabel(ItemName);

            if (costBadgeText != null)
                costBadgeText.text = "";

            if (categoryColorBar != null)
                categoryColorBar.color = GetCategoryColor(Category);

            SetSelected(false);
        }

        /// <summary>
        /// Resolves the preview sprite for any buildable data or tile asset (including RuleTiles and custom tiles).
        /// </summary>
        public static Sprite ResolveSprite(TmbsBuildableData data, TileBase tile)
        {
            if (data != null && data.previewSprite != null)
                return data.previewSprite;

            if (tile == null)
                return null;

            if (tile is Tile standardTile && standardTile.sprite != null)
                return standardTile.sprite;

            if (tile is RuleTile ruleTile && ruleTile.m_DefaultSprite != null)
                return ruleTile.m_DefaultSprite;

            try
            {
                var td = new TileData();
                tile.GetTileData(Vector3Int.zero, null, ref td);
                if (td.sprite != null)
                    return td.sprite;
            }
            catch
            {
                // Fallback safe
            }

            return null;
        }

        private static Color GetCategoryColor(string category)
        {
            if (string.IsNullOrEmpty(category)) return new Color(0.4f, 0.6f, 0.8f, 1f);
            switch (category.ToLowerInvariant())
            {
                case "bloques": return new Color(0.2f, 0.7f, 1f, 1f);
                case "estructura": return new Color(1f, 0.65f, 0.2f, 1f);
                case "tiles": return new Color(0.4f, 0.9f, 0.5f, 1f);
                case "armas":
                case "weapons": return new Color(1f, 0.3f, 0.3f, 1f);
                default: return new Color(0.6f, 0.7f, 0.8f, 1f);
            }
        }

        /// <summary>
        /// Toggles visual selection highlight on this slot.
        /// </summary>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (highlightBorder != null)
            {
                highlightBorder.enabled = true;
                highlightBorder.color = selected ? selectedColor : normalBorderColor;
            }

            // Slight scale effect for selected item
            transform.localScale = selected ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
        }

        private void SetIcon(Sprite sprite)
        {
            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }
        }

        private void SetLabel(string text)
        {
            if (labelText != null)
            {
                labelText.text = text;
            }
        }

        private void OnClick()
        {
            _onSelectedCallback?.Invoke(this);
        }

        /// <summary>
        /// Allows procedural construction of the slot UI elements if instantiated dynamically.
        /// </summary>
        public void SetInternalReferences(Image icon, Text label, Image border, Button btn, Text costBadge = null)
        {
            iconImage = icon;
            labelText = label;
            highlightBorder = border;
            slotButton = btn;
            costBadgeText = costBadge;
        }
    }
}
