using System;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace TMBS.UI
{
    /// <summary>
    /// Palette slot representing a selectable buildable or direct tile in a 3-zone card layout.
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
        [SerializeField] private GameObject costBadgeContainer;
        [SerializeField] private GameObject hotkeyBadge;
        [SerializeField] private Text hotkeyText;
        [SerializeField] private LayoutElement slotLayoutElement;

        [Header("State (Legacy Serialization Preserved)")]
        [SerializeField] private Color selectedColor = new Color(0.96f, 0.65f, 0.14f, 1f);
        [SerializeField] private Color normalBorderColor = new Color(0.25f, 0.25f, 0.3f, 0.6f);

        public TmbsBuildableData BuildableData { get; private set; }
        public TileBase DirectTile { get; private set; }
        public bool IsSelected { get; private set; }
        public string ItemName { get; private set; }
        public string Category { get; private set; }
        public bool IsDirectTileSlot => BuildableData == null && DirectTile != null;

        private Action<TmbsPaletteSlot> _onSelectedCallback;
        private Outline _slotOutline;
        private TmbsUIThemeSettings _theme;
        private bool _clickListenerBound;

        private void Awake()
        {
            EnsureSlotStructure();
            BindClickHandler();
        }

        private void BindClickHandler()
        {
            if (_clickListenerBound)
                return;

            if (slotButton == null)
                slotButton = GetComponent<Button>();

            if (slotButton != null)
            {
                slotButton.onClick.AddListener(OnClick);
                _clickListenerBound = true;
            }
        }

        private TmbsUIThemeSettings EnsureTheme()
        {
            return _theme ?? (_theme = new TmbsUIThemeSettings());
        }

        private void EnsureSlotStructure()
        {
            RemoveLegacyAspectRatioFitter();

            if (slotButton == null)
                slotButton = GetComponent<Button>();

            NormalizeButtonColors();
            EnsureLayoutElement();

            bool isLegacyPrefab = categoryColorBar == null;
            EnsureCategoryColorBar(isLegacyPrefab);
            EnsureCostBadgeContainer();
            EnsureVisualAccessories();
            EnsureHotkeyBadge();

            if (highlightBorder != null)
                highlightBorder.enabled = IsSelected;
        }

        private void RemoveLegacyAspectRatioFitter()
        {
            var arf = GetComponent<AspectRatioFitter>();
            if (arf == null)
                return;

            // Disable first so same-frame layout passes immediately respect LayoutElement.
            arf.enabled = false;
            if (Application.isPlaying)
                Destroy(arf);
            else
                DestroyImmediate(arf);
        }

        private void NormalizeButtonColors()
        {
            if (slotButton == null)
                return;

            var colors = slotButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            slotButton.colors = colors;
        }

        private void EnsureLayoutElement()
        {
            if (slotLayoutElement != null)
                return;

            slotLayoutElement = GetComponent<LayoutElement>();
            if (slotLayoutElement == null)
            {
                slotLayoutElement = gameObject.AddComponent<LayoutElement>();
                slotLayoutElement.minWidth = 76f;
                slotLayoutElement.minHeight = 80f;
                slotLayoutElement.preferredWidth = 84f;
                slotLayoutElement.preferredHeight = 88f;
                slotLayoutElement.flexibleWidth = 0f;
                slotLayoutElement.flexibleHeight = 0f;
            }
        }

        private void EnsureCategoryColorBar(bool isLegacyPrefab)
        {
            if (categoryColorBar == null)
            {
                Transform existing = transform.Find("CategoryColorBar");
                if (existing != null)
                {
                    var existingImg = existing.GetComponent<Image>();
                    if (existingImg != null && existingImg != highlightBorder)
                        categoryColorBar = existingImg;
                }
            }

            if (categoryColorBar == null)
            {
                var barObj = new GameObject("CategoryColorBar", typeof(RectTransform), typeof(Image));
                barObj.transform.SetParent(transform, false);

                var barRt = barObj.GetComponent<RectTransform>();
                SetStretchRect(barRt, new Vector2(0f, 0f), new Vector2(1f, 0.045f));

                categoryColorBar = barObj.GetComponent<Image>();
                categoryColorBar.raycastTarget = false;
                categoryColorBar.color = EnsureTheme().ResolveCategoryColor(Category);
            }

            if (!isLegacyPrefab)
                return;

            // Upgrade legacy prefab anchors without overwriting Inspector edits on updated prefabs.
            if (highlightBorder != null && highlightBorder != categoryColorBar && highlightBorder.rectTransform.anchorMax.y <= 0.1f)
            {
                SetStretchRect(highlightBorder.rectTransform, new Vector2(0f, 0.955f), new Vector2(1f, 1f));
                highlightBorder.fillCenter = true;
            }

            if (iconImage != null)
            {
                iconImage.preserveAspect = true;
                var iconRt = iconImage.rectTransform;
                if (iconRt.anchorMax.y > 0.85f || iconRt.anchorMin.y < 0.27f)
                {
                    SetStretchRect(iconRt, new Vector2(0.15f, 0.28f), new Vector2(0.85f, 0.76f));
                }
            }

            if (labelText != null && labelText.rectTransform.anchorMin.y < 0.045f)
            {
                SetStretchRect(labelText.rectTransform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.26f));
            }
        }

        private void EnsureCostBadgeContainer()
        {
            if (costBadgeContainer != null || costBadgeText == null)
                return;

            if (costBadgeText.transform.parent != null && costBadgeText.transform.parent != transform)
            {
                costBadgeContainer = costBadgeText.transform.parent.gameObject;
                return;
            }

            var theme = EnsureTheme();
            Transform existing = transform.Find("CostBadge_Pill");
            if (existing != null)
            {
                costBadgeContainer = existing.gameObject;
            }
            else
            {
                costBadgeContainer = new GameObject("CostBadge_Pill", typeof(RectTransform), typeof(Image));
                costBadgeContainer.transform.SetParent(transform, false);
                SetStretchRect(costBadgeContainer.GetComponent<RectTransform>(), new Vector2(0.34f, 0.78f), new Vector2(0.94f, 0.95f));

                var pillImage = costBadgeContainer.GetComponent<Image>();
                pillImage.color = theme.badgePillColor;
                pillImage.raycastTarget = false;
            }

            costBadgeText.transform.SetParent(costBadgeContainer.transform, false);
            var textRt = costBadgeText.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.offsetMin = new Vector2(3f, 0f);
            textRt.offsetMax = new Vector2(-3f, 0f);

            costBadgeText.fontSize = 10;
            costBadgeText.resizeTextForBestFit = true;
            costBadgeText.resizeTextMinSize = 8;
            costBadgeText.resizeTextMaxSize = 11;
            costBadgeText.fontStyle = FontStyle.Bold;
            costBadgeText.alignment = TextAnchor.MiddleCenter;
            costBadgeText.color = theme.badgeTextColor;
            costBadgeText.raycastTarget = false;
            costBadgeText.supportRichText = false;
        }

        private void EnsureVisualAccessories()
        {
            var theme = EnsureTheme();
            _slotOutline = GetComponent<Outline>();
            if (_slotOutline == null)
            {
                _slotOutline = gameObject.AddComponent<Outline>();
                _slotOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            _slotOutline.effectColor = IsSelected ? theme.selectionBorderColor : theme.panelBorderColor;

            EnsureShadowIfMissing(gameObject, new Vector2(0f, -2f), new Color(0f, 0f, 0f, 0.45f));

            if (labelText != null)
                EnsureShadowIfMissing(labelText.gameObject, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.8f));
        }

        private static void EnsureShadowIfMissing(GameObject target, Vector2 effectDistance, Color effectColor)
        {
            if (target == null)
                return;

            var shadows = target.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                    return;
            }

            var shadow = target.AddComponent<Shadow>();
            shadow.effectDistance = effectDistance;
            shadow.effectColor = effectColor;
        }

        private void EnsureHotkeyBadge()
        {
            if (hotkeyBadge != null && hotkeyText != null)
                return;

            Transform existing = transform.Find("HotkeyBadge");
            if (existing != null)
            {
                hotkeyBadge = existing.gameObject;
                if (hotkeyText == null)
                    hotkeyText = existing.GetComponentInChildren<Text>(true);
                if (hotkeyText != null)
                    return;
            }

            var theme = EnsureTheme();
            if (hotkeyBadge == null)
            {
                hotkeyBadge = new GameObject("HotkeyBadge", typeof(RectTransform), typeof(Image));
                hotkeyBadge.transform.SetParent(transform, false);
                SetStretchRect(hotkeyBadge.GetComponent<RectTransform>(), new Vector2(0.06f, 0.78f), new Vector2(0.28f, 0.95f));

                var badgeImage = hotkeyBadge.GetComponent<Image>();
                badgeImage.color = theme.badgePillColor;
                badgeImage.raycastTarget = false;
                hotkeyBadge.SetActive(false);
            }

            if (hotkeyText == null)
            {
                var textObj = new GameObject("HotkeyText", typeof(RectTransform), typeof(Text));
                textObj.transform.SetParent(hotkeyBadge.transform, false);
                SetStretchRect(textObj.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

                hotkeyText = textObj.GetComponent<Text>();
                Font font = labelText != null ? labelText.font : null;
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                hotkeyText.font = font;
                hotkeyText.fontSize = 10;
                hotkeyText.resizeTextForBestFit = true;
                hotkeyText.resizeTextMinSize = 9;
                hotkeyText.resizeTextMaxSize = 11;
                hotkeyText.fontStyle = FontStyle.Bold;
                hotkeyText.alignment = TextAnchor.MiddleCenter;
                hotkeyText.color = theme.slotNormalLabelColor;
                hotkeyText.raycastTarget = false;
            }
        }

        public void SetHotkeyIndex(int index)
        {
            EnsureHotkeyBadge();

            if (hotkeyBadge == null || hotkeyText == null)
                return;

            if (index >= 1 && index <= 9)
            {
                var theme = EnsureTheme();
                var badgeImage = hotkeyBadge.GetComponent<Image>();
                if (badgeImage != null)
                    badgeImage.color = theme.badgePillColor;

                hotkeyText.color = theme.slotNormalLabelColor;
                hotkeyBadge.SetActive(true);
                hotkeyText.text = index.ToString();
            }
            else
            {
                hotkeyBadge.SetActive(false);
            }
        }

        public void Setup(TmbsBuildableData data, Action<TmbsPaletteSlot> onSelected)
        {
            Setup(data, onSelected, null);
        }

        public void Setup(TmbsBuildableData data, Action<TmbsPaletteSlot> onSelected, TmbsUIThemeSettings theme)
        {
            _theme = theme ?? new TmbsUIThemeSettings();
            EnsureCostBadgeContainer();

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

                string metaText = _theme.FormatSlotMetaBadge(data);
                UpdateCostBadge(metaText);
            }
            else
            {
                ItemName = "None";
                Category = "None";
                SetIcon(null);
                SetLabel("None");
                UpdateCostBadge(string.Empty);
            }

            ApplyCategoryColorBar();
            SetSelected(false);
        }

        /// <summary>
        /// Configures this slot from a TileBase asset.
        /// </summary>
        public void Setup(TileBase tile, string displayName, Sprite customIcon, string category, Action<TmbsPaletteSlot> onSelected)
        {
            Setup(tile, displayName, customIcon, category, onSelected, null);
        }

        /// <summary>
        /// Configures this slot from a TileBase asset using the supplied theme settings.
        /// </summary>
        public void Setup(
            TileBase tile,
            string displayName,
            Sprite customIcon,
            string category,
            Action<TmbsPaletteSlot> onSelected,
            TmbsUIThemeSettings theme)
        {
            _theme = theme ?? new TmbsUIThemeSettings();
            EnsureCostBadgeContainer();

            BuildableData = null;
            DirectTile = tile;
            _onSelectedCallback = onSelected;
            ItemName = !string.IsNullOrEmpty(displayName) ? displayName : (tile != null ? tile.name : "Tile");
            Category = !string.IsNullOrEmpty(category) ? category : "Tiles";

            Sprite icon = customIcon != null ? customIcon : ResolveSprite(null, tile);
            SetIcon(icon);
            SetLabel(ItemName);
            UpdateCostBadge(string.Empty);
            ApplyCategoryColorBar();
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

        public void SetSelected(bool selected)
        {
            var theme = EnsureTheme();
            IsSelected = selected;

            ApplyCategoryColorBar();

            if (highlightBorder != null)
            {
                highlightBorder.enabled = selected;
                highlightBorder.color = theme.selectionBorderColor;
            }

            Graphic background = slotButton != null && slotButton.targetGraphic != null
                ? slotButton.targetGraphic
                : GetComponent<Image>();
            if (background != null)
            {
                background.color = selected ? theme.slotSelectedColor : theme.slotNormalColor;
            }

            if (_slotOutline == null)
                _slotOutline = GetComponent<Outline>();
            if (_slotOutline != null)
            {
                _slotOutline.effectColor = selected ? theme.selectionBorderColor : theme.panelBorderColor;
            }

            if (labelText != null)
            {
                labelText.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                labelText.color = selected ? theme.slotSelectedLabelColor : theme.slotNormalLabelColor;
            }

            transform.localScale = Vector3.one;
        }

        private void ApplyCategoryColorBar()
        {
            if (categoryColorBar != null)
            {
                categoryColorBar.color = EnsureTheme().ResolveCategoryColor(Category);
            }
        }

        private void UpdateCostBadge(string metaText)
        {
            var theme = EnsureTheme();
            bool hasMeta = !string.IsNullOrEmpty(metaText);

            if (costBadgeText != null)
            {
                costBadgeText.text = metaText;
                costBadgeText.color = theme.badgeTextColor;
            }

            if (costBadgeContainer != null)
            {
                var pillImage = costBadgeContainer.GetComponent<Image>();
                if (pillImage != null)
                    pillImage.color = theme.badgePillColor;

                costBadgeContainer.SetActive(hasMeta);
            }
            else if (costBadgeText != null)
            {
                costBadgeText.gameObject.SetActive(hasMeta);
            }
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

        private static void SetStretchRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rt == null)
                return;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Assigns UI element references for dynamically instantiated slots.
        /// </summary>
        public void SetInternalReferences(
            Image icon,
            Text label,
            Image border,
            Button btn,
            Text costBadge = null,
            Image categoryBar = null,
            GameObject costContainer = null,
            GameObject hotkeyBadgeObj = null,
            Text hotkeyTxt = null,
            LayoutElement layoutElem = null)
        {
            iconImage = icon;
            labelText = label;
            highlightBorder = border;
            slotButton = btn;
            costBadgeText = costBadge;
            categoryColorBar = categoryBar;
            costBadgeContainer = costContainer;
            hotkeyBadge = hotkeyBadgeObj;
            hotkeyText = hotkeyTxt;
            slotLayoutElement = layoutElem;
        }
    }
}
