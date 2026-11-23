using System;
using System.Collections;
using System.Collections.Generic;
using TMBS.Core.Events;
using TMBS.Core.Intents;
using TMBS.Core.Pending;
using TMBS.Core.Validation;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TMBS.UI
{
    /// <summary>
    /// Controls the runtime building palette, category filter tabs, active item HUD, and status feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public class TmbsBuildingUI : MonoBehaviour
    {
        [Header("TMBS Core Link")]
        [Tooltip("Reference to the BuildeableTilemap component in the scene. If unassigned, will be auto-detected.")]
        [SerializeField] private BuildeableTilemap buildeableTilemap;

        [Header("Palette Catalog")]
        [Tooltip("List of Buildable Data assets displayed in the palette.")]
        [SerializeField] private List<TmbsBuildableData> buildables = new List<TmbsBuildableData>();

        [Tooltip("Optional list of direct TileBase assets to display alongside buildable data.")]
        [SerializeField] private List<TileBase> directTiles = new List<TileBase>();

        [Header("UI Containers")]
        [SerializeField] private Transform paletteContainer;
        [SerializeField] private TmbsPaletteSlot slotPrefab;
        [SerializeField] private Transform categoryTabsContainer;

        [Header("Active Selection HUD")]
        [SerializeField] private Image activeItemIcon;
        [SerializeField] private Text activeItemNameText;
        [SerializeField] private Text activeItemDetailsText;
        [SerializeField] private GameObject activeItemPanel;

        [Header("Action Controls")]
        [SerializeField] private Button rotateButton;
        [SerializeField] private Button mirrorButton;
        [SerializeField] private Button eraseButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button redoButton;

        [Header("Validation Feedback (Toast)")]
        [SerializeField] private GameObject toastPanel;
        [SerializeField] private Text toastText;
        [SerializeField] private float toastDuration = 2.5f;

        [Header("Pending Construction Status")]
        [SerializeField] private GameObject pendingPanel;
        [SerializeField] private Text pendingCountText;

        [Header("Shortcuts")]
        [SerializeField] private bool enableKeyboardShortcuts = true;

        [Header("Visual Theme & Formatting")]
        [SerializeField] private TmbsUIThemeSettings theme = new TmbsUIThemeSettings();

        [Header("Responsive Dock & Scroll (Auto-Resolved if Unassigned)")]
        [SerializeField] private RectTransform bottomDockPanel;
        [SerializeField] private ScrollRect paletteScrollRect;
        [SerializeField] private ScrollRect categoryTabsScrollRect;
        [SerializeField] private Button toggleExpandCatalogButton;
        [SerializeField] private Text titleText;
        [SerializeField] private Text paletteHeaderText;
        [SerializeField] private float compactDockHeight = 168f;
        [SerializeField] private float expandedDockHeight = 340f;

#if ENABLE_INPUT_SYSTEM
        private static readonly Key[] DigitKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        private static readonly Key[] NumpadKeys =
        {
            Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5,
            Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
        };
#endif

        private readonly List<TmbsPaletteSlot> _instantiatedSlots = new List<TmbsPaletteSlot>();
        private readonly List<string> _categories = new List<string> { "All" };
        private readonly List<string> _tabCategories = new List<string>();
        private readonly List<Button> _instantiatedCategoryButtons = new List<Button>();
        private string _activeCategory = "All";
        private Coroutine _toastCoroutine;
        private bool _hasUserClearedSelection;
        private bool _hasInitializedPalette;
        private Font _defaultFont;
        private float _lastValidationToastTime = -1f;
        private Vector3Int _lastInspectedCell;
        private bool _hasInspectedCell;

        // Subscriptions
        private IDisposable _selectionChangedSub;
        private IDisposable _validationFailedSub;
        private IDisposable _pendingCreatedSub;
        private IDisposable _pendingChangedSub;
        private IDisposable _pendingCompletedSub;

        public BuildeableTilemap BuildeableTilemap => buildeableTilemap;
        public IReadOnlyList<TmbsBuildableData> Buildables => buildables;
        public TmbsUIThemeSettings Theme => theme ?? (theme = new TmbsUIThemeSettings());
        public bool IsCatalogExpanded { get; private set; }

        /// <summary>Toggles between compact horizontal row and expanded multi-row grid catalog modes.</summary>
        public void ToggleCatalogExpanded() => SetCatalogExpanded(!IsCatalogExpanded);

        /// <summary>Switches the catalog dock between compact 1-row horizontal scroll and expanded multi-row grid.</summary>
        public void SetCatalogExpanded(bool expanded)
        {
            IsCatalogExpanded = expanded;

            if (bottomDockPanel != null)
            {
                if (Mathf.Approximately(bottomDockPanel.anchorMin.y, bottomDockPanel.anchorMax.y))
                {
                    bottomDockPanel.sizeDelta = new Vector2(
                        bottomDockPanel.sizeDelta.x,
                        expanded ? expandedDockHeight : compactDockHeight);
                }
                else
                {
                    bottomDockPanel.anchorMax = new Vector2(
                        bottomDockPanel.anchorMax.x,
                        bottomDockPanel.anchorMin.y + (expanded ? 0.32f : 0.16f));
                    bottomDockPanel.offsetMin = Vector2.zero;
                    bottomDockPanel.offsetMax = Vector2.zero;
                }
            }

            if (paletteContainer != null)
            {
                var containerRt = paletteContainer as RectTransform ?? paletteContainer.GetComponent<RectTransform>();

                if (!expanded)
                {
                    RemoveIncompatibleLayoutGroups<HorizontalLayoutGroup>(paletteContainer.gameObject);

                    var hLayout = paletteContainer.GetComponent<HorizontalLayoutGroup>()
                        ?? paletteContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hLayout.padding = new RectOffset(6, 6, 4, 4);
                    hLayout.spacing = 10f;
                    hLayout.childAlignment = TextAnchor.MiddleLeft;
                    hLayout.childControlWidth = true;
                    hLayout.childControlHeight = true;
                    hLayout.childForceExpandWidth = false;
                    hLayout.childForceExpandHeight = true;

                    var fitter = paletteContainer.GetComponent<ContentSizeFitter>()
                        ?? paletteContainer.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

                    if (containerRt != null)
                    {
                        containerRt.anchorMin = new Vector2(0f, 0f);
                        containerRt.anchorMax = new Vector2(0f, 1f);
                        containerRt.pivot = new Vector2(0f, 0.5f);
                        containerRt.anchoredPosition = Vector2.zero;
                        containerRt.sizeDelta = new Vector2(containerRt.sizeDelta.x, 0f);
                    }

                    if (paletteScrollRect != null)
                    {
                        paletteScrollRect.horizontal = true;
                        paletteScrollRect.vertical = false;
                        paletteScrollRect.horizontalNormalizedPosition = 0f;
                    }
                }
                else
                {
                    RemoveIncompatibleLayoutGroups<GridLayoutGroup>(paletteContainer.gameObject);

                    var grid = paletteContainer.GetComponent<GridLayoutGroup>()
                        ?? paletteContainer.gameObject.AddComponent<GridLayoutGroup>();
                    grid.padding = new RectOffset(8, 8, 6, 6);
                    grid.cellSize = new Vector2(84f, 88f);
                    grid.spacing = new Vector2(10f, 10f);
                    grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                    grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                    grid.childAlignment = TextAnchor.UpperLeft;
                    grid.constraint = GridLayoutGroup.Constraint.Flexible;

                    var fitter = paletteContainer.GetComponent<ContentSizeFitter>()
                        ?? paletteContainer.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    if (containerRt != null)
                    {
                        containerRt.anchorMin = new Vector2(0f, 1f);
                        containerRt.anchorMax = new Vector2(1f, 1f);
                        containerRt.pivot = new Vector2(0.5f, 1f);
                        containerRt.anchoredPosition = Vector2.zero;
                        containerRt.sizeDelta = new Vector2(0f, containerRt.sizeDelta.y);
                    }

                    if (paletteScrollRect != null)
                    {
                        paletteScrollRect.horizontal = false;
                        paletteScrollRect.vertical = true;
                        paletteScrollRect.verticalNormalizedPosition = 1f;
                    }
                }
            }

            if (toggleExpandCatalogButton != null)
            {
                var btnLabel = toggleExpandCatalogButton.GetComponentInChildren<Text>(true);
                if (btnLabel != null)
                {
                    btnLabel.text = expanded ? "Collapse \u229F" : "Expand \u229E";
                }
            }
        }

        private void Awake()
        {
            EnsureEventSystemExists();
            ResolveBuildeableTilemap();
            EnsureRuntimeBindingsAndFallbacks();
            ApplyThemeToPanelsAndButtons();
            HookButtons();

            if (paletteContainer != null)
            {
                SetCatalogExpanded(false);
            }
        }

        private void OnEnable()
        {
            SubscribeToTmbsEvents();
        }

        private void Start()
        {
            ResolveBuildeableTilemap();

            if (paletteContainer == null)
            {
                Debug.LogWarning("TMBS: Palette container is not assigned. Use the TmbsBuildingUI prefab or Tools/TMBS/1. Configure Scene UI.", this);
            }

            if (_selectionChangedSub == null)
            {
                SubscribeToTmbsEvents();
            }

            BuildCategoryTabs();
            BuildPalette();
            RefreshCurrentSelectionDisplay();
            UpdatePendingCount();
            RefreshEraseButtonVisual();
            UpdateUndoRedoInteractability();
            RefreshActionShortcutLabels();
        }

        private void OnDisable()
        {
            UnsubscribeFromTmbsEvents();

            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
                _toastCoroutine = null;
            }
        }

        private void Update()
        {
            // Lazy reconnect if BuildeableTilemap was composed after UI initialization
            if (_selectionChangedSub == null && buildeableTilemap != null && buildeableTilemap.Events != null)
            {
                SubscribeToTmbsEvents();
            }

            if (enableKeyboardShortcuts)
            {
                HandlePaletteHotkeys();
            }
        }

        private void EnsureRuntimeBindingsAndFallbacks()
        {
            if (titleText == null && activeItemPanel != null)
            {
                titleText = FindChildText(
                    activeItemPanel.transform,
                    "TitleText",
                    "Title",
                    "LeftInfo_Zone/InfoText_Stack/HeaderRow/TitleText");
            }

            if (bottomDockPanel == null)
            {
                bottomDockPanel = ResolveBottomDockPanel();
            }

            if (paletteHeaderText == null && bottomDockPanel != null)
            {
                paletteHeaderText = FindChildText(
                    bottomDockPanel,
                    "PaletteTitle",
                    "PaletteHeader",
                    "DockHeader/PaletteTitle",
                    "DockHeader/PaletteHeader");
            }

            if (toggleExpandCatalogButton == null && bottomDockPanel != null)
            {
                toggleExpandCatalogButton = FindChildComponent<Button>(
                    bottomDockPanel,
                    "BtnExpandCatalog",
                    "DockHeader/BtnExpandCatalog",
                    "BtnExpand");
            }

            if (paletteScrollRect == null && paletteContainer != null)
            {
                paletteScrollRect = EnsureHorizontalScrollRect(paletteContainer, "Slots_ScrollView", 28f);
            }

            if (categoryTabsScrollRect == null && categoryTabsContainer != null)
            {
                categoryTabsScrollRect = EnsureHorizontalScrollRect(categoryTabsContainer, "CategoryTabs_ScrollView", 24f);
            }

            EnsureActiveIconAspectFallback();
            EnsureLegacyToolbarButtons();
            ApplyPixelPerfectAndTextCrispness();
        }

        private void ApplyPixelPerfectAndTextCrispness()
        {
            var rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                rootCanvas.pixelPerfect = true;

                var scaler = rootCanvas.GetComponent<CanvasScaler>();
                if (scaler != null &&
                    scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                    Mathf.Approximately(scaler.referenceResolution.x, 1920f) &&
                    Mathf.Approximately(scaler.referenceResolution.y, 1080f))
                {
                    scaler.referenceResolution = new Vector2(1280f, 720f);
                }
            }

            DisableBestFit(titleText);
            DisableBestFit(activeItemNameText);
            DisableBestFit(activeItemDetailsText);
            DisableBestFit(pendingCountText);
            DisableBestFit(toastText);
            DisableBestFit(paletteHeaderText);

            DisableBestFitForButton(rotateButton);
            DisableBestFitForButton(mirrorButton);
            DisableBestFitForButton(eraseButton);
            DisableBestFitForButton(clearButton);
            DisableBestFitForButton(undoButton);
            DisableBestFitForButton(redoButton);
            DisableBestFitForButton(toggleExpandCatalogButton);
        }

        private static void DisableBestFit(Text txt)
        {
            if (txt != null)
                txt.resizeTextForBestFit = false;
        }

        private static void DisableBestFitForButton(Button btn)
        {
            if (btn == null)
                return;

            DisableBestFit(btn.GetComponentInChildren<Text>(true));
        }

        private void ApplyThemeToPanelsAndButtons()
        {
            var t = Theme;

            ApplyPanelSurface(activeItemPanel, t.panelBackgroundColor, t.panelBorderColor);
            if (bottomDockPanel != null)
                ApplyPanelSurface(bottomDockPanel.gameObject, t.panelBackgroundColor, t.panelBorderColor);
            ApplyPanelSurface(pendingPanel, t.pendingPanelColor, t.pendingAccentColor);

            if (titleText != null)
                titleText.color = t.headerAccentColor;

            ApplyButtonTheme(rotateButton, t.neutralButtonColor, t.panelBorderColor);
            ApplyButtonTheme(mirrorButton, t.neutralButtonColor, t.panelBorderColor);
            ApplyButtonTheme(undoButton, t.neutralButtonColor, t.panelBorderColor);
            ApplyButtonTheme(redoButton, t.neutralButtonColor, t.panelBorderColor);
            ApplyButtonTheme(clearButton, t.clearButtonColor, t.panelBorderColor);
            ApplyButtonTheme(toggleExpandCatalogButton, t.neutralButtonColor, t.panelBorderColor);
        }

        private static void ApplyPanelSurface(GameObject panelObj, Color bgColor, Color borderColor)
        {
            if (panelObj == null)
                return;

            var img = panelObj.GetComponent<Image>();
            if (img != null)
                img.color = bgColor;

            EnsureOutline(panelObj, borderColor);
        }

        private static void ApplyButtonTheme(Button btn, Color bgColor, Color borderColor)
        {
            if (btn == null)
                return;

            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = bgColor;

            ApplyNormalizedButtonColors(btn);
            EnsureOutline(btn.gameObject, borderColor);
        }

        private void RefreshActionShortcutLabels()
        {
            var inputCfg = buildeableTilemap != null && buildeableTilemap.RootConfig != null
                ? buildeableTilemap.RootConfig.input
                : null;

            if (inputCfg == null || inputCfg.mode != TmbsInputMode.Legacy)
                return;

            var bindings = inputCfg.GetLegacyBindings();
            if (bindings == null)
                return;

            SetButtonShortcutLabel(rotateButton, "Rotate", bindings.rotate);
            SetButtonShortcutLabel(mirrorButton, "Mirror", bindings.mirror);
            SetButtonShortcutLabel(undoButton, "Undo", bindings.undo);
            SetButtonShortcutLabel(redoButton, "Redo", bindings.redo);
        }

        private static void SetButtonShortcutLabel(Button btn, string baseLabel, TmbsActionBinding binding)
        {
            if (btn == null)
                return;

            var label = btn.GetComponentInChildren<Text>(true);
            if (label == null)
                return;

            var slot = binding.primary.IsBound ? binding.primary : binding.secondary;
            string keyText = slot.IsBound ? slot.ToDisplayString() : string.Empty;
            label.text = !string.IsNullOrEmpty(keyText)
                ? $"{baseLabel} [{keyText}]"
                : baseLabel;
        }

        private RectTransform ResolveBottomDockPanel()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Transform named = canvas.transform.Find("BottomPalette_Panel");
                if (named is RectTransform namedRt)
                    return namedRt;
            }

            if (paletteContainer == null)
                return null;

            Transform cur = paletteContainer.parent;
            while (cur != null && cur.GetComponent<Canvas>() == null)
            {
                if (!IsScrollOrViewportTransform(cur) && cur is RectTransform dockRt)
                    return dockRt;

                cur = cur.parent;
            }

            return null;
        }

        private static bool IsScrollOrViewportTransform(Transform t)
        {
            if (t == null)
                return false;

            if (t.GetComponent<ScrollRect>() != null ||
                t.GetComponent<Mask>() != null ||
                t.GetComponent<RectMask2D>() != null)
            {
                return true;
            }

            return t.name.IndexOf("Viewport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   t.name.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ScrollRect EnsureHorizontalScrollRect(Transform container, string wrapperName, float scrollSensitivity)
        {
            if (container == null)
                return null;

            var existingScroll = container.GetComponentInParent<ScrollRect>();
            if (existingScroll != null)
            {
                if (existingScroll.content == null && container is RectTransform existingContentRt)
                    existingScroll.content = existingContentRt;

                return existingScroll;
            }

            var containerRt = container as RectTransform ?? container.GetComponent<RectTransform>();
            if (containerRt == null || containerRt.parent == null)
                return null;

            Transform originalParent = containerRt.parent;
            int siblingIndex = containerRt.GetSiblingIndex();

            var scrollObj = new GameObject(
                wrapperName,
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            scrollObj.transform.SetParent(originalParent, false);
            scrollObj.transform.SetSiblingIndex(siblingIndex);
            scrollObj.layer = container.gameObject.layer;

            var scrollRt = scrollObj.GetComponent<RectTransform>();
            scrollRt.anchorMin = containerRt.anchorMin;
            scrollRt.anchorMax = containerRt.anchorMax;
            scrollRt.pivot = containerRt.pivot;
            scrollRt.anchoredPosition = containerRt.anchoredPosition;
            scrollRt.sizeDelta = containerRt.sizeDelta;
            scrollRt.localScale = Vector3.one;

            var raycastBg = scrollObj.GetComponent<Image>();
            raycastBg.color = new Color(0f, 0f, 0f, 0f);
            raycastBg.raycastTarget = true;

            var innerMask = containerRt.GetComponent<RectMask2D>();
            if (innerMask != null)
            {
                DestroyImmediate(innerMask);
            }

            containerRt.SetParent(scrollObj.transform, false);
            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(0f, 1f);
            containerRt.pivot = new Vector2(0f, 0.5f);
            containerRt.anchoredPosition = Vector2.zero;
            containerRt.sizeDelta = Vector2.zero;

            var scrollRect = scrollObj.GetComponent<ScrollRect>();
            scrollRect.viewport = scrollRt;
            scrollRect.content = containerRt;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = scrollSensitivity;

            return scrollRect;
        }

        private void EnsureActiveIconAspectFallback()
        {
            if (activeItemIcon == null)
                return;

            activeItemIcon.preserveAspect = true;

            Transform iconParent = activeItemIcon.transform.parent;
            bool hasParentFrame = iconParent != null &&
                (iconParent.GetComponent<AspectRatioFitter>() != null ||
                 iconParent.GetComponent<LayoutGroup>() != null ||
                 string.Equals(iconParent.name, "ActiveIcon_Frame", StringComparison.OrdinalIgnoreCase));

            if (!hasParentFrame && activeItemIcon.GetComponent<AspectRatioFitter>() == null)
            {
                var fitter = activeItemIcon.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                fitter.aspectRatio = 1f;
            }
        }

        private void EnsureLegacyToolbarButtons()
        {
            if (activeItemPanel == null)
                return;

            bool needsAny = rotateButton == null || mirrorButton == null || eraseButton == null;
            if (!needsAny)
                return;

            Font font = ResolveToolbarFont();
            var t = Theme;

            if (rotateButton == null)
            {
                rotateButton = ResolveOrCreateLegacyToolbarButton(
                    "BtnRotate",
                    "Rotate [R]",
                    0.500f,
                    0.578f,
                    t.neutralButtonColor,
                    t.panelBorderColor,
                    font);
            }

            if (mirrorButton == null)
            {
                mirrorButton = ResolveOrCreateLegacyToolbarButton(
                    "BtnMirror",
                    "Mirror [F]",
                    0.582f,
                    0.660f,
                    t.neutralButtonColor,
                    t.panelBorderColor,
                    font);
            }

            if (eraseButton == null)
            {
                eraseButton = ResolveOrCreateLegacyToolbarButton(
                    "BtnErase",
                    "Erase [X]",
                    0.664f,
                    0.744f,
                    t.eraseIdleColor,
                    t.panelBorderColor,
                    font);
            }
        }

        private Button ResolveOrCreateLegacyToolbarButton(
            string objectName,
            string labelText,
            float minX,
            float maxX,
            Color bgColor,
            Color borderColor,
            Font font)
        {
            var existing = FindChildComponent<Button>(
                activeItemPanel.transform,
                objectName,
                "ToolbarActions_Container/" + objectName);
            if (existing != null)
                return existing;

            var btnObj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(activeItemPanel.transform, false);
            btnObj.layer = activeItemPanel.layer;

            SetAnchorRect(btnObj.GetComponent<RectTransform>(), new Vector2(minX, 0.16f), new Vector2(maxX, 0.84f));

            var img = btnObj.GetComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;

            EnsureOutline(btnObj, borderColor);
            EnsureShadowIfMissing(btnObj, new Vector2(0f, -2f), new Color(0f, 0f, 0f, 0.45f));

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;
            ApplyNormalizedButtonColors(btn);

            var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(btnObj.transform, false);
            labelObj.layer = btnObj.layer;
            SetAnchorRect(labelObj.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

            var label = labelObj.GetComponent<Text>();
            label.text = labelText;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = font;
            label.raycastTarget = false;
            ConfigureTextElement(label, 13, 9, 14);

            return btn;
        }

        private static Text FindChildText(Transform root, params string[] relativePaths)
        {
            return FindChildComponent<Text>(root, relativePaths);
        }

        private static T FindChildComponent<T>(Transform root, params string[] relativePaths) where T : Component
        {
            if (root == null || relativePaths == null)
                return null;

            for (int i = 0; i < relativePaths.Length; i++)
            {
                Transform child = root.Find(relativePaths[i]);
                if (child != null)
                {
                    var comp = child.GetComponent<T>();
                    if (comp != null)
                        return comp;
                }
            }

            return null;
        }

        private static void RemoveIncompatibleLayoutGroups<TKeep>(GameObject target) where TKeep : LayoutGroup
        {
            var groups = target.GetComponents<LayoutGroup>();
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && !(groups[i] is TKeep))
                {
                    DestroyImmediate(groups[i]);
                }
            }
        }

        private static void ApplyNormalizedButtonColors(Button button)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
        }

        private static Outline EnsureOutline(GameObject target, Color effectColor)
        {
            if (target == null)
                return null;

            var outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
                outline.effectDistance = new Vector2(1f, -1f);
            }
            outline.effectColor = effectColor;
            return outline;
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

        private static void SetAnchorRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rt == null)
                return;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void ConfigureTextElement(Text text, int fontSize, int minSize, int maxSize)
        {
            if (text == null)
                return;

            text.fontSize = fontSize;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
        }

        private void ResolveBuildeableTilemap()
        {
            if (buildeableTilemap != null)
                return;

#if UNITY_2023_1_OR_NEWER
            buildeableTilemap = FindAnyObjectByType<BuildeableTilemap>();
#else
            buildeableTilemap = FindObjectOfType<BuildeableTilemap>();
#endif
        }

        private void SubscribeToTmbsEvents()
        {
            ResolveBuildeableTilemap();

            if (buildeableTilemap == null || buildeableTilemap.Events == null)
                return;

            UnsubscribeFromTmbsEvents();

            var events = buildeableTilemap.Events;
            _selectionChangedSub = events.Subscribe<SelectionChangedEvent>(OnTmbsSelectionChanged);
            _validationFailedSub = events.Subscribe<ValidationFailedEvent>(OnTmbsValidationFailed);
            _pendingCreatedSub = events.Subscribe<PendingCreatedEvent>(OnTmbsPendingUpdated);
            _pendingChangedSub = events.Subscribe<PendingChangedEvent>(OnTmbsPendingUpdated);
            _pendingCompletedSub = events.Subscribe<PendingCompletedEvent>(OnTmbsPendingUpdated);

            buildeableTilemap.IntentObserved -= OnTilemapIntentObserved;
            buildeableTilemap.IntentObserved += OnTilemapIntentObserved;
        }

        private void UnsubscribeFromTmbsEvents()
        {
            if (buildeableTilemap != null)
                buildeableTilemap.IntentObserved -= OnTilemapIntentObserved;

            _selectionChangedSub?.Dispose();
            _selectionChangedSub = null;

            _validationFailedSub?.Dispose();
            _validationFailedSub = null;

            _pendingCreatedSub?.Dispose();
            _pendingCreatedSub = null;

            _pendingChangedSub?.Dispose();
            _pendingChangedSub = null;

            _pendingCompletedSub?.Dispose();
            _pendingCompletedSub = null;
        }

        private void OnTmbsSelectionChanged(SelectionChangedEvent evt)
        {
            if (buildeableTilemap != null && evt.InstanceId != buildeableTilemap.InstanceId)
                return;

            var displayData = ResolveDisplayDataFallback(evt.Selection.BuildableData, evt.Selection.BuildTile);
            RefreshActiveItemDisplay(displayData, evt.Selection.BuildTile);
            HighlightSlotForSelection(evt.Selection.BuildableData, evt.Selection.BuildTile);
            RefreshEraseButtonVisual();
            UpdatePendingCount();
        }

        private void OnTmbsValidationFailed(ValidationFailedEvent evt)
        {
            if (buildeableTilemap != null && evt.InstanceId != buildeableTilemap.InstanceId)
                return;

            if (evt.Failure == ValidationFailure.None)
                return;

            if (!IsPrimaryPointerActiveOrReleased())
                return;

            if (Time.unscaledTime - _lastValidationToastTime < 0.35f)
                return;

            _lastValidationToastTime = Time.unscaledTime;

            string message;
            switch (evt.Failure)
            {
                case ValidationFailure.MissingSelection:
                    message = "Select a buildable first.";
                    break;
                case ValidationFailure.Occupied:
                    message = "Cell is occupied.";
                    break;
                case ValidationFailure.OutOfBounds:
                    message = "Out of bounds.";
                    break;
                default:
                    message = "Invalid placement.";
                    break;
            }

            ShowToast($"[!] {message}", true);
        }

        private static bool IsPrimaryPointerActiveOrReleased()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null &&
                (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasReleasedThisFrame))
            {
                return true;
            }
            if (Pointer.current != null &&
                (Pointer.current.press.isPressed || Pointer.current.press.wasReleasedThisFrame))
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
            {
                return true;
            }
#endif
            return false;
        }

        private void OnTmbsPendingUpdated(PendingCreatedEvent evt)
        {
            if (buildeableTilemap != null && evt.InstanceId != buildeableTilemap.InstanceId) return;
            UpdatePendingCount();
        }

        private void OnTmbsPendingUpdated(PendingChangedEvent evt)
        {
            if (buildeableTilemap != null && evt.InstanceId != buildeableTilemap.InstanceId) return;
            UpdatePendingCount();
        }

        private void OnTmbsPendingUpdated(PendingCompletedEvent evt)
        {
            if (buildeableTilemap != null && evt.InstanceId != buildeableTilemap.InstanceId) return;
            UpdatePendingCount();
        }

        private static string NormalizeCategory(string rawCategory)
        {
            return string.IsNullOrWhiteSpace(rawCategory) ? "General" : rawCategory.Trim();
        }

        public void BuildCategoryTabs()
        {
            _categories.Clear();
            _categories.Add("All");

            var categoryCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int totalCount = 0;

            if (buildables != null)
            {
                foreach (var b in buildables)
                {
                    if (b == null)
                        continue;

                    string cat = NormalizeCategory(b.category);
                    if (!_categories.Contains(cat))
                    {
                        _categories.Add(cat);
                    }

                    categoryCounts[cat] = categoryCounts.TryGetValue(cat, out int count) ? count + 1 : 1;
                    totalCount++;
                }
            }

            if (directTiles != null)
            {
                foreach (var t in directTiles)
                {
                    if (t == null)
                        continue;

                    if (!_categories.Contains("Tiles"))
                    {
                        _categories.Add("Tiles");
                    }

                    categoryCounts["Tiles"] = categoryCounts.TryGetValue("Tiles", out int count) ? count + 1 : 1;
                    totalCount++;
                }
            }

            categoryCounts["All"] = totalCount;

            if (!_categories.Contains(_activeCategory))
            {
                _activeCategory = "All";
            }

            if (categoryTabsContainer == null)
                return;

            for (int i = categoryTabsContainer.childCount - 1; i >= 0; i--)
            {
                var child = categoryTabsContainer.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
            _instantiatedCategoryButtons.Clear();
            _tabCategories.Clear();

            var tabsLayout = categoryTabsContainer.GetComponent<HorizontalLayoutGroup>()
                ?? categoryTabsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.padding = new RectOffset(4, 4, 2, 2);
            tabsLayout.spacing = 6f;
            tabsLayout.childAlignment = TextAnchor.MiddleLeft;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = false;
            tabsLayout.childForceExpandHeight = true;

            var tabsFitter = categoryTabsContainer.GetComponent<ContentSizeFitter>()
                ?? categoryTabsContainer.gameObject.AddComponent<ContentSizeFitter>();
            tabsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tabsFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            if (categoryTabsScrollRect != null && categoryTabsContainer is RectTransform tabsRt)
            {
                tabsRt.anchorMin = new Vector2(0f, 0f);
                tabsRt.anchorMax = new Vector2(0f, 1f);
                tabsRt.pivot = new Vector2(0f, 0.5f);
                tabsRt.anchoredPosition = Vector2.zero;
                tabsRt.sizeDelta = new Vector2(tabsRt.sizeDelta.x, 0f);
                categoryTabsScrollRect.horizontal = true;
                categoryTabsScrollRect.vertical = false;
            }

            Font font = ResolveDefaultFont();
            var themeSettings = Theme;

            foreach (var cat in _categories)
            {
                string categoryName = cat;
                int itemCount = categoryCounts.TryGetValue(categoryName, out int c) ? c : 0;
                string displayLabel = $"{categoryName} ({itemCount})";

                var tabObj = new GameObject(
                    $"Tab_{categoryName}",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(LayoutElement));
                tabObj.transform.SetParent(categoryTabsContainer, false);
                tabObj.layer = categoryTabsContainer.gameObject.layer;

                var layoutElement = tabObj.GetComponent<LayoutElement>();
                layoutElement.minWidth = 72f;
                layoutElement.preferredWidth = Mathf.Max(80f, displayLabel.Length * 8.5f + 24f);
                layoutElement.preferredHeight = 28f;

                var btn = tabObj.GetComponent<Button>();
                var bgImg = tabObj.GetComponent<Image>();
                btn.targetGraphic = bgImg;
                ApplyNormalizedButtonColors(btn);
                EnsureOutline(tabObj, themeSettings.panelBorderColor);

                btn.onClick.AddListener(() => SetCategoryFilter(categoryName));

                var indicatorObj = new GameObject("CategoryIndicator", typeof(RectTransform), typeof(Image));
                indicatorObj.transform.SetParent(tabObj.transform, false);
                indicatorObj.layer = tabObj.layer;
                var indRt = indicatorObj.GetComponent<RectTransform>();
                indRt.anchorMin = new Vector2(0f, 0f);
                indRt.anchorMax = new Vector2(1f, 0f);
                indRt.pivot = new Vector2(0.5f, 0f);
                indRt.anchoredPosition = Vector2.zero;
                indRt.sizeDelta = new Vector2(0f, 2.5f);
                var indImg = indicatorObj.GetComponent<Image>();
                indImg.raycastTarget = false;
                indImg.color = string.Equals(categoryName, "All", StringComparison.Ordinal)
                    ? themeSettings.headerAccentColor
                    : themeSettings.ResolveCategoryColor(categoryName);

                var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObj.transform.SetParent(tabObj.transform, false);
                labelObj.layer = tabObj.layer;
                var labelRt = labelObj.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0f, 0.08f);
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(6f, 1f);
                labelRt.offsetMax = new Vector2(-6f, -1f);

                var txt = labelObj.GetComponent<Text>();
                txt.text = displayLabel;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = font;
                txt.raycastTarget = false;
                ConfigureTextElement(txt, 12, 9, 13);

                _instantiatedCategoryButtons.Add(btn);
                _tabCategories.Add(categoryName);
            }

            RefreshCategoryTabVisuals();
        }

        private void RefreshCategoryTabVisuals()
        {
            var t = Theme;

            for (int i = 0; i < _instantiatedCategoryButtons.Count; i++)
            {
                var btn = _instantiatedCategoryButtons[i];
                if (btn == null)
                    continue;

                string categoryName = i < _tabCategories.Count ? _tabCategories[i] : null;
                bool isActive = string.Equals(categoryName, _activeCategory, StringComparison.Ordinal);

                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = isActive ? t.tabActiveColor : t.tabInactiveColor;
                }

                EnsureOutline(btn.gameObject, isActive ? t.selectionBorderColor : t.panelBorderColor);

                var label = btn.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
                    label.color = isActive ? t.slotSelectedLabelColor : t.slotNormalLabelColor;
                }
            }
        }

        public void SetCategoryFilter(string category)
        {
            _activeCategory = string.IsNullOrWhiteSpace(category) ? "All" : category.Trim();
            RefreshCategoryTabVisuals();
            BuildPalette();
        }

        public void BuildPalette()
        {
            if (paletteContainer == null)
                return;

            for (int i = paletteContainer.childCount - 1; i >= 0; i--)
            {
                var child = paletteContainer.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
            _instantiatedSlots.Clear();

            var themeSettings = Theme;

            if (buildables != null && buildables.Count > 0)
            {
                var sortedBuildables = new List<TmbsBuildableData>(buildables.Count);
                for (int i = 0; i < buildables.Count; i++)
                {
                    if (buildables[i] != null)
                        sortedBuildables.Add(buildables[i]);
                }

                sortedBuildables.Sort((a, b) => TmbsBuildableData.Compare(a, b, TmbsBuildableSortMode.CatalogOrder));

                foreach (var data in sortedBuildables)
                {
                    string cat = NormalizeCategory(data.category);
                    if (!string.Equals(_activeCategory, "All", StringComparison.Ordinal) &&
                        !string.Equals(cat, _activeCategory, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var slot = CreateSlotInstance();
                    if (slot == null)
                        continue;

                    slot.Setup(data, OnSlotClicked, themeSettings);
                    _instantiatedSlots.Add(slot);
                }
            }

            if (directTiles != null)
            {
                foreach (var tile in directTiles)
                {
                    if (tile == null)
                        continue;

                    if (!string.Equals(_activeCategory, "All", StringComparison.Ordinal) &&
                        !string.Equals(_activeCategory, "Tiles", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var slot = CreateSlotInstance();
                    if (slot == null)
                        continue;

                    slot.Setup(tile, tile.name, null, "Tiles", OnSlotClicked, themeSettings);
                    _instantiatedSlots.Add(slot);
                }
            }

            for (int i = 0; i < _instantiatedSlots.Count; i++)
            {
                _instantiatedSlots[i].SetHotkeyIndex(i < 9 ? i + 1 : 0);
            }

            if (paletteHeaderText != null)
            {
                paletteHeaderText.text = $"BUILD CATALOG ({_instantiatedSlots.Count})  \u00B7  Hotkeys 1-9";
            }

            TileBase curTile = null;
            TmbsBuildableData curData = null;
            if (buildeableTilemap != null && buildeableTilemap.CurrentBuildable != null)
            {
                curTile = buildeableTilemap.CurrentBuildable.ResolvePrimaryTile();
                curData = buildeableTilemap.CurrentBuildable.ResolveData();
            }

            if (curData != null || curTile != null)
            {
                var resolvedDisplayData = ResolveDisplayDataFallback(curData, curTile);
                HighlightSlotForSelection(curData, curTile);
                RefreshActiveItemDisplay(curData ?? resolvedDisplayData, curTile);
            }
            else if (!_hasInitializedPalette &&
                     !_hasUserClearedSelection &&
                     _instantiatedSlots.Count > 0 &&
                     (buildeableTilemap == null || !buildeableTilemap.IsEraseMode))
            {
                OnSlotClicked(_instantiatedSlots[0]);
            }
            else
            {
                HighlightSlot(null);
                RefreshCurrentSelectionDisplay();
            }

            _hasInitializedPalette = true;
        }

        private TmbsBuildableData ResolveDisplayDataFallback(TmbsBuildableData data, TileBase tile)
        {
            if (data != null || tile == null)
                return data;

            if (directTiles != null && directTiles.Contains(tile))
                return null;

            if (buildables != null)
            {
                for (int i = 0; i < buildables.Count; i++)
                {
                    var b = buildables[i];
                    if (b != null && b.tile == tile)
                        return b;
                }
            }

            return null;
        }

        private TmbsPaletteSlot CreateSlotInstance()
        {
            if (slotPrefab == null)
            {
                Debug.LogWarning("TMBS: Slot prefab is not assigned on TmbsBuildingUI.", this);
                return null;
            }

            return Instantiate(slotPrefab, paletteContainer);
        }

        private void OnSlotClicked(TmbsPaletteSlot clickedSlot)
        {
            if (clickedSlot == null || buildeableTilemap == null)
                return;

            _hasUserClearedSelection = false;

            if (clickedSlot.BuildableData != null)
            {
                buildeableTilemap.SetBuildableData(clickedSlot.BuildableData);
            }
            else if (clickedSlot.DirectTile != null)
            {
                buildeableTilemap.SetBuildTile(clickedSlot.DirectTile);
            }

            HighlightSlot(clickedSlot);
            RefreshEraseButtonVisual();
        }

        private void HighlightSlot(TmbsPaletteSlot targetSlot)
        {
            foreach (var slot in _instantiatedSlots)
            {
                if (slot != null)
                {
                    slot.SetSelected(slot == targetSlot);
                }
            }
        }

        private void HighlightSlotForSelection(TmbsBuildableData data, TileBase tile)
        {
            TmbsPaletteSlot matched = null;

            if (data != null)
            {
                for (int i = 0; i < _instantiatedSlots.Count; i++)
                {
                    var slot = _instantiatedSlots[i];
                    if (slot != null && slot.BuildableData == data)
                    {
                        matched = slot;
                        break;
                    }
                }
            }
            else if (tile != null)
            {
                for (int i = 0; i < _instantiatedSlots.Count; i++)
                {
                    var slot = _instantiatedSlots[i];
                    if (slot != null && slot.IsDirectTileSlot && slot.DirectTile == tile)
                    {
                        matched = slot;
                        break;
                    }
                }

                if (matched == null)
                {
                    for (int i = 0; i < _instantiatedSlots.Count; i++)
                    {
                        var slot = _instantiatedSlots[i];
                        if (slot != null && slot.DirectTile == tile)
                        {
                            matched = slot;
                            break;
                        }
                    }
                }
            }

            HighlightSlot(matched);
        }

        private void RefreshCurrentSelectionDisplay()
        {
            var current = buildeableTilemap != null ? buildeableTilemap.CurrentBuildable : null;
            var curData = current != null ? current.ResolveData() : null;
            var curTile = current != null ? current.ResolvePrimaryTile() : null;
            RefreshActiveItemDisplay(ResolveDisplayDataFallback(curData, curTile), curTile);
        }

        private void RefreshActiveItemDisplay(TmbsBuildableData data, TileBase tile)
        {
            UpdateUndoRedoInteractability();

            bool isErase = buildeableTilemap != null && buildeableTilemap.IsEraseMode;
            if (isErase)
            {
                _hasInspectedCell = false;
                if (rotateButton != null) rotateButton.interactable = false;
                if (mirrorButton != null) mirrorButton.interactable = false;

                if (titleText != null) titleText.text = "ERASE MODE";
                if (activeItemNameText != null) activeItemNameText.text = "Mode: Erase";
                bool isPendingMode = buildeableTilemap.RootConfig != null &&
                                     buildeableTilemap.RootConfig.executionMode == ExecutionMode.Pending;
                if (activeItemDetailsText != null)
                {
                    activeItemDetailsText.text = isPendingMode
                        ? "Pending: Click or drag to cancel orders or queue demolition."
                        : "Immediate: Click or drag to erase cells.";
                }
                if (activeItemIcon != null)
                {
                    activeItemIcon.enabled = false;
                    activeItemIcon.rectTransform.localEulerAngles = Vector3.zero;
                    activeItemIcon.rectTransform.localScale = Vector3.one;
                }
                return;
            }

            if (data == null && tile == null)
            {
                if (rotateButton != null) rotateButton.interactable = false;
                if (mirrorButton != null) mirrorButton.interactable = false;

                if (titleText != null) titleText.text = "INSPECT MODE";
                if (_hasInspectedCell)
                {
                    InspectCell(_lastInspectedCell);
                    return;
                }

                if (activeItemNameText != null) activeItemNameText.text = "Mode: Inspect";
                if (activeItemDetailsText != null) activeItemDetailsText.text = "Hover or click a placed tile to inspect, or pick a buildable below.";
                if (activeItemIcon != null)
                {
                    activeItemIcon.enabled = false;
                    activeItemIcon.rectTransform.localEulerAngles = Vector3.zero;
                    activeItemIcon.rectTransform.localScale = Vector3.one;
                }
                return;
            }

            _hasInspectedCell = false;
            if (rotateButton != null) rotateButton.interactable = data == null || data.canRotate;
            if (mirrorButton != null) mirrorButton.interactable = data == null || data.canMirror;

            if (titleText != null) titleText.text = "BUILD MODE";

            string displayName = data != null
                ? data.GetEffectiveDisplayName()
                : tile.name;
            string category = data != null ? data.GetEffectiveCategory() : "Tiles";
            string sub = data != null ? data.GetEffectiveSubcategory() : string.Empty;
            int work = data != null ? data.workCost : 1;
            int res = data != null ? data.resourceCost : 0;
            Vector2Int sz = data != null ? data.GetSize() : Vector2Int.one;

            if (activeItemNameText != null)
            {
                activeItemNameText.text = displayName;
            }

            var t = Theme;
            bool isPending = buildeableTilemap != null &&
                             buildeableTilemap.RootConfig != null &&
                             buildeableTilemap.RootConfig.executionMode == ExecutionMode.Pending;

            var parts = new List<string>(8);
            parts.Add($"Size: {sz.x}\u00D7{sz.y}");
            parts.Add(string.IsNullOrEmpty(sub) ? category : $"{category} / {sub}");
            if (data != null)
            {
                parts.Add(data.placementRole.ToString());
                if (data.tier > 0)
                    parts.Add($"T{data.tier}");
                if (data.maxHealth > 0)
                    parts.Add($"HP:{data.maxHealth}");
            }
            if (!t.hideZeroCostInHud || isPending || work > 1)
                parts.Add($"Work: {work}");
            if (!t.hideZeroCostInHud || res > 0)
                parts.Add($"Cost: {t.resourceCostPrefix}{res}");

            int rotation = 0;
            bool mirrored = false;
            if (buildeableTilemap != null)
            {
                var tr = buildeableTilemap.CurrentTransform;
                rotation = tr.Rotation;
                mirrored = tr.Mirrored;
                if (rotation != 0)
                    parts.Add($"{rotation * 90}\u00B0");
                if (mirrored)
                    parts.Add("Mirrored");
            }

            string summary = string.Join("  \u00B7  ", parts);
            if (data != null && !string.IsNullOrWhiteSpace(data.description))
                summary = $"{summary}  \u2014  {data.description.Trim()}";

            if (activeItemDetailsText != null)
            {
                activeItemDetailsText.text = summary;
            }

            if (activeItemIcon != null)
            {
                Sprite icon = TmbsPaletteSlot.ResolveSprite(data, tile);
                activeItemIcon.sprite = icon;
                activeItemIcon.enabled = icon != null;
                activeItemIcon.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f * rotation);
                activeItemIcon.rectTransform.localScale = new Vector3(mirrored ? -1f : 1f, 1f, 1f);
            }
        }

        private TmbsBuildableData GetActiveBuildableData()
        {
            var current = buildeableTilemap != null ? buildeableTilemap.CurrentBuildable : null;
            return current != null ? current.ResolveData() : null;
        }

        private void OnTilemapIntentObserved(BuildIntent intent)
        {
            if (buildeableTilemap == null || buildeableTilemap.TargetTilemap == null)
                return;

            bool isInspecting = !buildeableTilemap.IsEraseMode &&
                                (buildeableTilemap.CurrentBuildable == null ||
                                 buildeableTilemap.CurrentBuildable.ResolvePrimaryTile() == null);
            if (!isInspecting)
                return;

            if (intent.Type != BuildIntentType.PointMove && intent.Type != BuildIntentType.Confirm)
                return;

            Vector3Int cell = buildeableTilemap.TargetTilemap.WorldToCell(intent.WorldPoint);
            if (intent.Type == BuildIntentType.PointMove && _hasInspectedCell && _lastInspectedCell == cell)
                return;

            _hasInspectedCell = true;
            _lastInspectedCell = cell;
            InspectCell(cell);
        }

        private void InspectCell(Vector3Int cell)
        {
            if (titleText != null) titleText.text = "INSPECT MODE";

            if (buildeableTilemap != null && buildeableTilemap.TryGetBuildRecord(cell, out var record))
            {
                var data = ResolveDisplayDataFallback(record.BuildableData, record.PlacedTile);
                string displayName = data != null
                    ? data.GetEffectiveDisplayName()
                    : (record.PlacedTile != null ? record.PlacedTile.name : "Placed Tile");

                var parts = new List<string>(6)
                {
                    $"Cell ({cell.x}, {cell.y})",
                    data != null ? data.GetEffectiveCategory() : "Tiles",
                    $"State: {record.State}"
                };

                if (record.FootprintBounds.size.x > 1 || record.FootprintBounds.size.y > 1)
                    parts.Add($"Footprint: {record.FootprintBounds.size.x}\u00D7{record.FootprintBounds.size.y}");

                if (record.CurrentHealth > 0)
                    parts.Add($"HP: {record.CurrentHealth}");

                if (activeItemNameText != null) activeItemNameText.text = displayName;
                if (activeItemDetailsText != null) activeItemDetailsText.text = string.Join("  \u00B7  ", parts);

                if (activeItemIcon != null)
                {
                    Sprite icon = TmbsPaletteSlot.ResolveSprite(data, record.PlacedTile);
                    activeItemIcon.sprite = icon;
                    activeItemIcon.enabled = icon != null;
                    activeItemIcon.rectTransform.localEulerAngles = Vector3.zero;
                    activeItemIcon.rectTransform.localScale = Vector3.one;
                }
                return;
            }

            if (buildeableTilemap?.PendingWorkApi != null &&
                buildeableTilemap.PendingWorkApi.TryGetCellStatus(cell, out var pendingCell))
            {
                var data = ResolveDisplayDataFallback(pendingCell.BuildableData, pendingCell.Tile);
                string displayName = data != null
                    ? data.GetEffectiveDisplayName()
                    : (pendingCell.Tile != null ? pendingCell.Tile.name : "Pending Construction");

                var parts = new List<string>(4)
                {
                    $"Cell ({cell.x}, {cell.y})",
                    $"Work: {pendingCell.WorkDone}/{pendingCell.WorkRequired}"
                };

                if (pendingCell.ResourceRequired > 0)
                    parts.Add($"Res: {pendingCell.ResourceDelivered}/{pendingCell.ResourceRequired}");

                if (activeItemNameText != null) activeItemNameText.text = $"[Pending] {displayName}";
                if (activeItemDetailsText != null) activeItemDetailsText.text = string.Join("  \u00B7  ", parts);

                if (activeItemIcon != null)
                {
                    Sprite icon = TmbsPaletteSlot.ResolveSprite(data, pendingCell.Tile);
                    activeItemIcon.sprite = icon;
                    activeItemIcon.enabled = icon != null;
                    activeItemIcon.rectTransform.localEulerAngles = Vector3.zero;
                    activeItemIcon.rectTransform.localScale = Vector3.one;
                }
                return;
            }

            TileBase looseTile = buildeableTilemap != null && buildeableTilemap.TargetTilemap != null
                ? buildeableTilemap.TargetTilemap.GetTile(cell)
                : null;
            if (looseTile != null)
            {
                if (activeItemNameText != null) activeItemNameText.text = looseTile.name;
                if (activeItemDetailsText != null) activeItemDetailsText.text = $"Existing Tile  \u00B7  Cell ({cell.x}, {cell.y})";

                if (activeItemIcon != null)
                {
                    Sprite icon = TmbsPaletteSlot.ResolveSprite(null, looseTile);
                    activeItemIcon.sprite = icon;
                    activeItemIcon.enabled = icon != null;
                    activeItemIcon.rectTransform.localEulerAngles = Vector3.zero;
                    activeItemIcon.rectTransform.localScale = Vector3.one;
                }
                return;
            }

            if (activeItemNameText != null) activeItemNameText.text = "Mode: Inspect";
            if (activeItemDetailsText != null) activeItemDetailsText.text = $"Empty cell ({cell.x}, {cell.y})  \u00B7  Hover or click a placed tile to inspect.";
            if (activeItemIcon != null)
            {
                activeItemIcon.enabled = false;
                activeItemIcon.rectTransform.localEulerAngles = Vector3.zero;
                activeItemIcon.rectTransform.localScale = Vector3.one;
            }
        }

        private void UpdateUndoRedoInteractability()
        {
            bool enabled = buildeableTilemap?.RootConfig?.history == null ||
                           buildeableTilemap.RootConfig.history.enableUndoRedo;

            if (undoButton != null)
                undoButton.interactable = enabled;

            if (redoButton != null)
                redoButton.interactable = enabled;
        }

        public void ShowToast(string message, bool isWarning = false)
        {
            if (toastPanel == null || toastText == null)
                return;

            if (!gameObject.activeInHierarchy)
                return;

            var t = Theme;
            var img = toastPanel.GetComponent<Image>();
            if (img != null)
            {
                img.color = isWarning ? t.warningToastColor : t.infoToastColor;
            }
            EnsureOutline(toastPanel, isWarning ? t.warningBorderColor : t.panelBorderColor);

            toastText.text = message;
            toastPanel.SetActive(true);

            if (_toastCoroutine != null)
                StopCoroutine(_toastCoroutine);

            _toastCoroutine = StartCoroutine(HideToastRoutine(toastDuration));
        }

        private IEnumerator HideToastRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (toastPanel != null)
            {
                toastPanel.SetActive(false);
            }
            _toastCoroutine = null;
        }

        private void UpdatePendingCount()
        {
            if (buildeableTilemap != null && buildeableTilemap.PendingWorkApi != null)
            {
                var all = buildeableTilemap.PendingWorkApi.GetAll();
                int count = all != null ? all.Count : 0;

                if (pendingCountText != null)
                    pendingCountText.text = $"Pending orders: {count}";

                if (pendingPanel != null)
                    pendingPanel.SetActive(count > 0);
            }
            else
            {
                if (pendingPanel != null)
                    pendingPanel.SetActive(false);
            }
        }

        private void HookButtons()
        {
            HookButton(rotateButton, OnRotateClicked);
            HookButton(mirrorButton, OnMirrorClicked);
            HookButton(eraseButton, OnEraseClicked);
            HookButton(clearButton, OnClearClicked);
            HookButton(undoButton, OnUndoClicked);
            HookButton(redoButton, OnRedoClicked);
            HookButton(toggleExpandCatalogButton, ToggleCatalogExpanded);
        }

        private static void HookButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(handler);
        }

        private Font ResolveToolbarFont()
        {
            if (undoButton != null)
            {
                var undoLabel = undoButton.GetComponentInChildren<Text>(true);
                if (undoLabel != null && undoLabel.font != null)
                    return undoLabel.font;
            }
            return ResolveDefaultFont();
        }

        private Font ResolveDefaultFont()
        {
            if (_defaultFont == null)
            {
                _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_defaultFont == null)
                {
                    _defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 12);
                }
            }
            return _defaultFont;
        }

        private void RefreshEraseButtonVisual()
        {
            if (eraseButton == null)
                return;

            bool active = buildeableTilemap != null && buildeableTilemap.IsEraseMode;
            var t = Theme;

            var img = eraseButton.GetComponent<Image>();
            if (img != null)
            {
                img.color = active ? t.eraseActiveColor : t.eraseIdleColor;
                if (eraseButton.targetGraphic == null)
                    eraseButton.targetGraphic = img;
            }

            if (eraseButton.colors.normalColor == Color.white && eraseButton.colors.selectedColor != Color.white)
            {
                var colors = eraseButton.colors;
                colors.selectedColor = Color.white;
                eraseButton.colors = colors;
            }

            EnsureOutline(eraseButton.gameObject, active ? t.selectionBorderColor : t.panelBorderColor);

            var label = eraseButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = active ? "ERASE ON [X]" : "Erase [X]";
            }
        }

        public void OnRotateClicked()
        {
            if (buildeableTilemap == null)
                return;

            var activeData = GetActiveBuildableData();
            if (activeData != null && !activeData.canRotate)
            {
                ShowToast("This buildable cannot be rotated.", true);
                return;
            }

            buildeableTilemap.Rotate();
            RefreshCurrentSelectionDisplay();

            var tr = buildeableTilemap.CurrentTransform;
            string mirrorStr = tr.Mirrored ? " \u00B7 Mirrored" : "";
            ShowToast($"Rotation: {tr.Rotation * 90}\u00B0{mirrorStr}", false);
        }

        public void OnMirrorClicked()
        {
            if (buildeableTilemap == null)
                return;

            var activeData = GetActiveBuildableData();
            if (activeData != null && !activeData.canMirror)
            {
                ShowToast("This buildable cannot be mirrored.", true);
                return;
            }

            buildeableTilemap.Mirror();
            RefreshCurrentSelectionDisplay();

            var tr = buildeableTilemap.CurrentTransform;
            ShowToast(tr.Mirrored ? $"Mirrored ({tr.Rotation * 90}\u00B0)" : $"Unmirrored ({tr.Rotation * 90}\u00B0)", false);
        }

        public void OnUndoClicked()
        {
            if (buildeableTilemap == null)
                return;

            if (buildeableTilemap.RootConfig?.history != null && !buildeableTilemap.RootConfig.history.enableUndoRedo)
            {
                ShowToast("Undo/Redo is disabled.");
                return;
            }

            bool success = buildeableTilemap.Undo();
            ShowToast(success ? "Undo" : "Nothing to undo.");
        }

        public void OnRedoClicked()
        {
            if (buildeableTilemap == null)
                return;

            if (buildeableTilemap.RootConfig?.history != null && !buildeableTilemap.RootConfig.history.enableUndoRedo)
            {
                ShowToast("Undo/Redo is disabled.");
                return;
            }

            bool success = buildeableTilemap.Redo();
            ShowToast(success ? "Redo" : "Nothing to redo.");
        }

        public void OnClearClicked()
        {
            _hasUserClearedSelection = true;
            if (buildeableTilemap != null)
            {
                buildeableTilemap.ClearSelection();
            }
            HighlightSlot(null);
            RefreshActiveItemDisplay(null, null);
            RefreshEraseButtonVisual();
            ShowToast("Selection cleared.");
        }

        public void OnEraseClicked()
        {
            if (buildeableTilemap == null)
                return;

            bool nextState = !buildeableTilemap.IsEraseMode;
            _hasUserClearedSelection = nextState;
            buildeableTilemap.SetEraseMode(nextState);

            if (nextState)
            {
                HighlightSlot(null);
            }

            RefreshCurrentSelectionDisplay();
            RefreshEraseButtonVisual();
            ShowToast(nextState ? "Erase mode enabled." : "Erase mode disabled.");
        }

        private static bool IsTypingInUiInputField()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
                return false;

            return selected.GetComponent<InputField>() != null ||
                   selected.GetComponent("TMP_InputField") != null;
        }

        private void HandlePaletteHotkeys()
        {
            if (buildeableTilemap?.RootConfig?.input != null &&
                buildeableTilemap.RootConfig.input.mode == TmbsInputMode.None)
            {
                return;
            }

            if (IsTypingInUiInputField())
                return;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard[Key.Escape].wasPressedThisFrame &&
                    buildeableTilemap != null &&
                    (buildeableTilemap.IsEraseMode || buildeableTilemap.CurrentBuildable != null))
                {
                    OnClearClicked();
                    return;
                }

                bool hasModifier = keyboard.ctrlKey.isPressed ||
                                   keyboard.altKey.isPressed ||
                                   keyboard.leftMetaKey.isPressed ||
                                   keyboard.rightMetaKey.isPressed;

                if (hasModifier)
                    return;

                if (keyboard.xKey.wasPressedThisFrame)
                {
                    OnEraseClicked();
                    return;
                }

                for (int i = 0; i < 9 && i < _instantiatedSlots.Count; i++)
                {
                    if (keyboard[DigitKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame)
                    {
                        OnSlotClicked(_instantiatedSlots[i]);
                        return;
                    }
                }
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape) &&
                buildeableTilemap != null &&
                (buildeableTilemap.IsEraseMode || buildeableTilemap.CurrentBuildable != null))
            {
                OnClearClicked();
                return;
            }

            bool legacyModifier = Input.GetKey(KeyCode.LeftControl) ||
                                  Input.GetKey(KeyCode.RightControl) ||
                                  Input.GetKey(KeyCode.LeftAlt) ||
                                  Input.GetKey(KeyCode.RightAlt) ||
                                  Input.GetKey(KeyCode.LeftCommand) ||
                                  Input.GetKey(KeyCode.RightCommand) ||
                                  Input.GetKey(KeyCode.LeftWindows) ||
                                  Input.GetKey(KeyCode.RightWindows);

            if (legacyModifier)
                return;

            if (Input.GetKeyDown(KeyCode.X))
            {
                OnEraseClicked();
                return;
            }

            for (int i = 0; i < 9 && i < _instantiatedSlots.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    OnSlotClicked(_instantiatedSlots[i]);
                    break;
                }
            }
#endif
        }

        private void EnsureEventSystemExists()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = FindAnyObjectByType<EventSystem>();
#else
            var existing = FindObjectOfType<EventSystem>();
#endif
            if (existing == null && EventSystem.current == null)
            {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
            }
        }
    }
}
