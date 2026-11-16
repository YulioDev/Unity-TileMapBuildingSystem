using System;
using UnityEngine;
using UnityEngine.UI;
using TMBS.Runtime.Config;
using UnityEngine.Tilemaps;

namespace TMBS.UI
{
    /// <summary>
    /// Palette slot representing a selectable buildable or tile.
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
        [SerializeField] private Color selectedColor = new Color(0.96f, 0.65f, 0.14f, 1f);
        [SerializeField] private Color normalBorderColor = new Color(0.25f, 0.25f, 0.3f, 0.6f);

        private static readonly Color NormalBackgroundColor = new Color(0.14f, 0.16f, 0.19f, 0.94f);
        private static readonly Color SelectedBackgroundColor = new Color(0.22f, 0.25f, 0.30f, 0.98f);
        private static readonly Color NormalOutlineColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color SelectedAccentColor = new Color(0.96f, 0.65f, 0.14f, 1f);
        private static readonly Color SelectedLabelColor = new Color(1f, 0.96f, 0.84f, 1f);
        private static readonly Color NormalLabelColor = new Color(0.92f, 0.94f, 0.96f, 1f);

        public TmbsBuildableData BuildableData { get; private set; }
        public TileBase DirectTile { get; private set; }
        public bool IsSelected { get; private set; }
        public string ItemName { get; private set; }
        public string Category { get; private set; }

        private Action<TmbsPaletteSlot> _onSelectedCallback;
        private Outline _slotOutline;
        private GameObject _hotkeyBadge;
        private Text _hotkeyText;

        private void Awake()
        {
            if (slotButton == null)
                slotButton = GetComponent<Button>();

            if (slotButton != null)
                slotButton.onClick.AddListener(OnClick);

            ConfigureSlotLayout();
        }

        private void ConfigureSlotLayout()
        {
            var fitter = GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = 1f;

            if (iconImage != null)
            {
                iconImage.preserveAspect = true;
                var iconRt = iconImage.rectTransform;
                iconRt.anchorMin = new Vector2(0.15f, 0.28f);
                iconRt.anchorMax = new Vector2(0.85f, 0.90f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;
            }

            if (highlightBorder != null)
            {
                var borderRt = highlightBorder.rectTransform;
                borderRt.anchorMin = new Vector2(0f, 0f);
                borderRt.anchorMax = new Vector2(1f, 0.05f);
                borderRt.offsetMin = Vector2.zero;
                borderRt.offsetMax = Vector2.zero;
                highlightBorder.fillCenter = true;
            }

            if (labelText != null)
            {
                labelText.fontSize = 13;
                labelText.resizeTextForBestFit = true;
                labelText.resizeTextMinSize = 9;
                labelText.resizeTextMaxSize = 14;
                labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
                labelText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            if (costBadgeText != null)
            {
                costBadgeText.fontSize = 12;
                costBadgeText.resizeTextForBestFit = true;
                costBadgeText.resizeTextMinSize = 9;
                costBadgeText.resizeTextMaxSize = 13;
            }

            EnsureVisualAccessories();
            EnsureHotkeyBadge();
        }

        private void EnsureVisualAccessories()
        {
            _slotOutline = GetComponent<Outline>();
            if (_slotOutline == null)
                _slotOutline = gameObject.AddComponent<Outline>();
            _slotOutline.effectDistance = new Vector2(1.5f, -1.5f);
            _slotOutline.effectColor = IsSelected ? SelectedAccentColor : NormalOutlineColor;

            EnsureShadow(gameObject, new Vector2(0f, -2f), new Color(0f, 0f, 0f, 0.45f));

            if (labelText != null)
                EnsureShadow(labelText.gameObject, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.8f));

            if (costBadgeText != null)
                EnsureShadow(costBadgeText.gameObject, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.8f));
        }

        private static void EnsureShadow(GameObject target, Vector2 effectDistance, Color effectColor)
        {
            if (target == null)
                return;

            Shadow shadow = null;
            var shadows = target.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    shadow = shadows[i];
                    break;
                }
            }

            if (shadow == null)
                shadow = target.AddComponent<Shadow>();

            shadow.effectDistance = effectDistance;
            shadow.effectColor = effectColor;
        }

        private void EnsureHotkeyBadge()
        {
            if (_hotkeyBadge != null && _hotkeyText != null)
                return;

            Transform existing = transform.Find("HotkeyBadge");
            if (existing != null)
            {
                _hotkeyBadge = existing.gameObject;
                _hotkeyText = existing.GetComponentInChildren<Text>(true);
                if (_hotkeyText != null)
                    return;
            }

            if (_hotkeyBadge == null)
            {
                _hotkeyBadge = new GameObject("HotkeyBadge", typeof(RectTransform), typeof(Image));
                _hotkeyBadge.transform.SetParent(transform, false);

                var badgeRt = _hotkeyBadge.GetComponent<RectTransform>();
                badgeRt.anchorMin = new Vector2(0.05f, 0.75f);
                badgeRt.anchorMax = new Vector2(0.28f, 0.95f);
                badgeRt.offsetMin = Vector2.zero;
                badgeRt.offsetMax = Vector2.zero;

                var badgeImage = _hotkeyBadge.GetComponent<Image>();
                badgeImage.color = new Color(0.07f, 0.08f, 0.10f, 0.82f);
                badgeImage.raycastTarget = false;

                _hotkeyBadge.SetActive(false);
            }

            var textObj = new GameObject("HotkeyText", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(_hotkeyBadge.transform, false);

            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            _hotkeyText = textObj.GetComponent<Text>();
            Font font = labelText != null ? labelText.font : null;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hotkeyText.font = font;
            _hotkeyText.fontSize = 11;
            _hotkeyText.resizeTextForBestFit = true;
            _hotkeyText.resizeTextMinSize = 9;
            _hotkeyText.resizeTextMaxSize = 12;
            _hotkeyText.fontStyle = FontStyle.Bold;
            _hotkeyText.alignment = TextAnchor.MiddleCenter;
            _hotkeyText.color = NormalLabelColor;
            _hotkeyText.raycastTarget = false;
        }

        public void SetHotkeyIndex(int index)
        {
            EnsureHotkeyBadge();

            if (_hotkeyBadge == null || _hotkeyText == null)
                return;

            if (index >= 1 && index <= 9)
            {
                _hotkeyBadge.SetActive(true);
                _hotkeyText.text = index.ToString();
            }
            else
            {
                _hotkeyBadge.SetActive(false);
            }
        }

        public void Setup(TmbsBuildableData data, Action<TmbsPaletteSlot> onSelected)
        {
            BuildableData = data;
            DirectTile = data != null ? data.tile : null;
            _onSelectedCallback = onSelected;

            if (data != null)
            {
                ItemName = !string.IsNullOrEmpty(data.displayName) ? data.displayName : data.name;
                Category = !string.IsNullOrEmpty(data.category) ? data.category : "General";

                Sprite icon = ResolveSprite(data, DirectTile);

                SetIcon(icon);
                SetLabel(ItemName);

                if (costBadgeText != null)
                {
                    Vector2Int size = data.GetSize();
                    string sizeTag = (data.isBlueprint && (size.x > 1 || size.y > 1)) ? $"{size.x}x{size.y}" : "";
                    if (data.resourceCost > 0)
                        costBadgeText.text = string.IsNullOrEmpty(sizeTag) ? $"${data.resourceCost}" : $"{sizeTag} ${data.resourceCost}";
                    else if (data.workCost > 1)
                        costBadgeText.text = string.IsNullOrEmpty(sizeTag) ? $"W:{data.workCost}" : $"{sizeTag} W:{data.workCost}";
                    else
                        costBadgeText.text = sizeTag;
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
        /// Configures this slot from a TileBase asset.
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
        /// Resolves the preview sprite for a buildable data or tile asset.
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
                case "blocks": return new Color(0.2f, 0.7f, 1f, 1f);
                case "hull": return new Color(0.35f, 0.65f, 0.9f, 1f);
                case "armor": return new Color(0.55f, 0.7f, 0.85f, 1f);
                case "structure": return new Color(1f, 0.65f, 0.2f, 1f);
                case "tiles": return new Color(0.4f, 0.9f, 0.5f, 1f);
                case "weapons": return new Color(1f, 0.3f, 0.3f, 1f);
                case "utility": return new Color(0.95f, 0.8f, 0.25f, 1f);
                case "engines": return new Color(1f, 0.45f, 0.2f, 1f);
                case "cockpits": return new Color(0.3f, 0.85f, 0.85f, 1f);
                case "connectors": return new Color(0.7f, 0.55f, 0.95f, 1f);
                case "decorative": return new Color(0.85f, 0.55f, 0.8f, 1f);
                default: return new Color(0.6f, 0.7f, 0.8f, 1f);
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (highlightBorder != null)
            {
                highlightBorder.enabled = true;
                highlightBorder.color = selected ? SelectedAccentColor : GetCategoryColor(Category);
            }

            Graphic background = slotButton != null && slotButton.targetGraphic != null
                ? slotButton.targetGraphic
                : GetComponent<Image>();
            if (background != null)
            {
                background.color = selected ? SelectedBackgroundColor : NormalBackgroundColor;
            }

            if (_slotOutline != null)
            {
                _slotOutline.effectColor = selected ? SelectedAccentColor : NormalOutlineColor;
            }

            if (labelText != null)
            {
                labelText.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                labelText.color = selected ? SelectedLabelColor : NormalLabelColor;
            }

            transform.localScale = Vector3.one;
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
        /// Assigns UI element references for dynamically instantiated slots.
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
