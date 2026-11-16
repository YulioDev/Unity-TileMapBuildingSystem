using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using TMBS.Core.Events;
using TMBS.Core.Validation;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
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

        private static readonly Color SurfacePanelColor = new Color(0.10f, 0.11f, 0.14f, 0.92f);
        private static readonly Color SurfaceOutlineColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color NeutralButtonColor = new Color(0.17f, 0.19f, 0.23f, 0.96f);
        private static readonly Color DestructiveIdleColor = new Color(0.28f, 0.16f, 0.17f, 0.96f);
        private static readonly Color EraseNormalColor = new Color(0.26f, 0.18f, 0.16f, 0.96f);
        private static readonly Color EraseActiveColor = new Color(0.78f, 0.22f, 0.22f, 1f);
        private static readonly Color ActiveTabColor = new Color(0.24f, 0.27f, 0.33f, 1f);
        private static readonly Color InactiveTabColor = new Color(0.13f, 0.15f, 0.18f, 0.88f);
        private static readonly Color AccentAmberColor = new Color(0.96f, 0.65f, 0.14f, 1f);
        private static readonly Color InfoToastColor = new Color(0.14f, 0.16f, 0.20f, 0.96f);
        private static readonly Color WarningToastColor = new Color(0.55f, 0.16f, 0.18f, 0.96f);

        private readonly List<TmbsPaletteSlot> _instantiatedSlots = new List<TmbsPaletteSlot>();
        private readonly List<string> _categories = new List<string> { "All" };
        private readonly List<Button> _instantiatedCategoryButtons = new List<Button>();
        private string _activeCategory = "All";
        private Coroutine _toastCoroutine;
        private bool _hasUserClearedSelection = false;
        private Font _defaultFont;
        private Text _titleText;
        private float _lastValidationToastTime = -1f;

        // Subscriptions
        private IDisposable _selectionChangedSub;
        private IDisposable _validationFailedSub;
        private IDisposable _pendingCreatedSub;
        private IDisposable _pendingChangedSub;
        private IDisposable _pendingCompletedSub;

        public BuildeableTilemap BuildeableTilemap => buildeableTilemap;
        public IReadOnlyList<TmbsBuildableData> Buildables => buildables;

        private void Awake()
        {
            EnsureEventSystemExists();
            ResolveBuildeableTilemap();

            if (paletteContainer == null)
            {
                Debug.LogWarning("TMBS: Palette container is not assigned. Use the TmbsBuildingUI prefab or Tools/TMBS/1. Configure Scene UI.", this);
            }

            ConfigureResponsiveCanvas();
            EnsureToolbarButtons();
            HookButtons();
        }

        private void ConfigureResponsiveCanvas()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.pixelPerfect = true;
            }

            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 64f;
            }

            if (activeItemPanel != null)
            {
                var topRt = activeItemPanel.GetComponent<RectTransform>();
                if (topRt != null)
                {
                    topRt.pivot = new Vector2(0.5f, 1f);
                    SetAnchorRect(topRt, new Vector2(0.015f, 0.905f), new Vector2(0.985f, 0.985f));
                }
                ApplySurfaceStyle(activeItemPanel, SurfacePanelColor, SurfaceOutlineColor);

                var titleTransform = activeItemPanel.transform.Find("TitleText") ?? activeItemPanel.transform.Find("Title");
                if (titleTransform != null)
                {
                    SetAnchorRect(titleTransform.GetComponent<RectTransform>(), new Vector2(0.06f, 0.54f), new Vector2(0.32f, 0.92f));
                    _titleText = titleTransform.GetComponent<Text>();
                    if (_titleText != null)
                    {
                        _titleText.color = AccentAmberColor;
                        ConfigureTextElement(_titleText, 16, 10, 18);
                    }
                }
            }

            if (activeItemIcon != null)
            {
                var iconRt = activeItemIcon.rectTransform;
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                SetAnchorRect(iconRt, new Vector2(0.01f, 0.12f), new Vector2(0.052f, 0.88f));
                activeItemIcon.preserveAspect = true;
            }

            if (activeItemNameText != null)
            {
                SetAnchorRect(activeItemNameText.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.24f, 0.52f));
                activeItemNameText.color = new Color(0.95f, 0.96f, 0.98f, 1f);
                ConfigureTextElement(activeItemNameText, 15, 10, 16);
            }

            if (activeItemDetailsText != null)
            {
                SetAnchorRect(activeItemDetailsText.rectTransform, new Vector2(0.245f, 0.08f), new Vector2(0.485f, 0.52f));
                activeItemDetailsText.fontStyle = FontStyle.Normal;
                activeItemDetailsText.color = new Color(0.75f, 0.78f, 0.83f, 1f);
                ConfigureTextElement(activeItemDetailsText, 13, 9, 14);
            }

            if (pendingPanel != null)
            {
                SetAnchorRect(pendingPanel.GetComponent<RectTransform>(), new Vector2(0.33f, 0.54f), new Vector2(0.485f, 0.92f));
                ApplySurfaceStyle(pendingPanel, new Color(0.20f, 0.16f, 0.07f, 0.96f), AccentAmberColor);
            }
            ConfigureTextElement(pendingCountText, 14, 9, 15);

            var bottomPanelRt = paletteContainer != null ? paletteContainer.parent as RectTransform : null;
            if (bottomPanelRt != null)
            {
                bottomPanelRt.pivot = new Vector2(0.5f, 0f);
                SetAnchorRect(bottomPanelRt, new Vector2(0.12f, 0.015f), new Vector2(0.88f, 0.175f));
                ApplySurfaceStyle(bottomPanelRt.gameObject, SurfacePanelColor, SurfaceOutlineColor);

                var paletteTitleTransform = bottomPanelRt.Find("PaletteTitle") ?? bottomPanelRt.Find("PaletteHeader");
                if (paletteTitleTransform != null)
                {
                    SetAnchorRect(paletteTitleTransform.GetComponent<RectTransform>(), new Vector2(0.02f, 0.80f), new Vector2(0.98f, 0.97f));
                    var paletteTitleText = paletteTitleTransform.GetComponent<Text>();
                    if (paletteTitleText != null)
                    {
                        paletteTitleText.color = new Color(0.88f, 0.90f, 0.94f, 1f);
                        ConfigureTextElement(paletteTitleText, 14, 10, 15);
                    }
                }
            }

            ConfigureMaskedContainer(categoryTabsContainer, new Vector2(0.02f, 0.60f), new Vector2(0.98f, 0.79f), 8f, true);
            ConfigureMaskedContainer(paletteContainer, new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.57f), 12f, false);

            if (toastPanel != null)
            {
                var toastRt = toastPanel.GetComponent<RectTransform>();
                if (toastRt != null)
                {
                    toastRt.pivot = new Vector2(0.5f, 0.5f);
                    SetAnchorRect(toastRt, new Vector2(0.28f, 0.19f), new Vector2(0.72f, 0.245f));
                }
                ApplySurfaceStyle(toastPanel, InfoToastColor, SurfaceOutlineColor);
            }
            ConfigureTextElement(toastText, 16, 10, 18);
        }

        private static void ConfigureMaskedContainer(Transform container, Vector2 anchorMin, Vector2 anchorMax, float spacing, bool controlChildWidth)
        {
            if (container == null) return;

            SetAnchorRect(container.GetComponent<RectTransform>(), anchorMin, anchorMax);
            if (container.GetComponent<RectMask2D>() == null)
            {
                container.gameObject.AddComponent<RectMask2D>();
            }

            var layout = container.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(8, 8, 4, 4);
                layout.childControlWidth = controlChildWidth;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                layout.spacing = spacing;
            }
        }

        private static void ApplySurfaceStyle(GameObject target, Color bgColor, Color outlineColor)
        {
            if (target == null) return;

            var img = target.GetComponent<Image>();
            if (img != null)
            {
                img.color = bgColor;
            }

            EnsureOutline(target, outlineColor);

            var shadow = EnsureExactShadow(target);
            shadow.effectDistance = new Vector2(0f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        }

        private static void ApplyButtonVisual(Button button, Color bgColor, Color outlineColor, int fontSize, int minSize, int maxSize)
        {
            if (button == null) return;

            var img = button.GetComponent<Image>();
            if (img != null)
            {
                img.color = bgColor;
                if (button.targetGraphic == null)
                {
                    button.targetGraphic = img;
                }
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            EnsureOutline(button.gameObject, outlineColor);

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                ConfigureTextElement(label, fontSize, minSize, maxSize);
                label.color = Color.white;
            }
        }

        private static Outline EnsureOutline(GameObject target, Color effectColor)
        {
            var outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1f, -1f);
            outline.effectColor = effectColor;
            return outline;
        }

        private static Shadow EnsureExactShadow(GameObject target)
        {
            var shadows = target.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    return shadows[i];
                }
            }
            return target.AddComponent<Shadow>();
        }

        private static void SetAnchorRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void ConfigureTextElement(Text text, int fontSize, int minSize, int maxSize)
        {
            if (text == null) return;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
        }

        private void OnEnable()
        {
            SubscribeToTmbsEvents();
        }

        private void Start()
        {
            ResolveBuildeableTilemap();

            if (_selectionChangedSub == null)
            {
                SubscribeToTmbsEvents();
            }

            BuildCategoryTabs();
            BuildPalette();
            RefreshCurrentSelectionDisplay();
            UpdatePendingCount();
            RefreshEraseButtonVisual();
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

        private void ResolveBuildeableTilemap()
        {
            if (buildeableTilemap != null) return;

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
        }

        private void UnsubscribeFromTmbsEvents()
        {
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

            RefreshActiveItemDisplay(evt.Selection.BuildableData, evt.Selection.BuildTile);
            HighlightSlotForSelection(evt.Selection.BuildableData, evt.Selection.BuildTile);
            RefreshEraseButtonVisual();
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

        public void BuildCategoryTabs()
        {
            _categories.Clear();
            _categories.Add("All");

            foreach (var b in buildables)
            {
                if (b != null && !string.IsNullOrEmpty(b.category) && !_categories.Contains(b.category))
                {
                    _categories.Add(b.category);
                }
            }

            foreach (var t in directTiles)
            {
                if (t != null && !_categories.Contains("Tiles"))
                {
                    _categories.Add("Tiles");
                }
            }

            if (categoryTabsContainer == null) return;

            for (int i = categoryTabsContainer.childCount - 1; i >= 0; i--)
            {
                var child = categoryTabsContainer.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
            _instantiatedCategoryButtons.Clear();

            Font font = ResolveDefaultFont();

            foreach (var cat in _categories)
            {
                string categoryName = cat;

                var tabObj = new GameObject($"Tab_{categoryName}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                tabObj.transform.SetParent(categoryTabsContainer, false);

                var rt = tabObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(85f, 24f);

                var layoutElement = tabObj.GetComponent<LayoutElement>();
                layoutElement.minWidth = 84f;
                layoutElement.preferredWidth = Mathf.Max(88f, categoryName.Length * 10f + 24f);
                layoutElement.preferredHeight = 28f;

                var btn = tabObj.GetComponent<Button>();
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                colors.selectedColor = Color.white;
                btn.colors = colors;

                EnsureOutline(tabObj, SurfaceOutlineColor);

                btn.onClick.AddListener(() => SetCategoryFilter(categoryName));

                var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObj.transform.SetParent(tabObj.transform, false);
                var labelRt = labelObj.GetComponent<RectTransform>();
                SetAnchorRect(labelRt, Vector2.zero, Vector2.one);

                var txt = labelObj.GetComponent<Text>();
                txt.text = categoryName;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = font;
                txt.raycastTarget = false;
                ConfigureTextElement(txt, 13, 9, 14);

                _instantiatedCategoryButtons.Add(btn);
            }

            RefreshCategoryTabVisuals();
        }

        private void RefreshCategoryTabVisuals()
        {
            for (int i = 0; i < _instantiatedCategoryButtons.Count; i++)
            {
                var btn = _instantiatedCategoryButtons[i];
                if (btn == null) continue;

                var label = btn.GetComponentInChildren<Text>();
                string categoryName = label != null
                    ? label.text
                    : (i < _categories.Count ? _categories[i] : null);
                bool isActive = categoryName == _activeCategory;

                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = isActive ? ActiveTabColor : InactiveTabColor;
                }

                EnsureOutline(btn.gameObject, isActive ? AccentAmberColor : SurfaceOutlineColor);

                if (label != null)
                {
                    label.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
                    label.color = isActive ? new Color(1f, 0.96f, 0.84f, 1f) : new Color(0.80f, 0.83f, 0.88f, 0.92f);
                }
            }
        }

        public void SetCategoryFilter(string category)
        {
            _activeCategory = category;
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

            foreach (var data in buildables)
            {
                if (data == null) continue;
                if (_activeCategory != "All" && data.category != _activeCategory) continue;

                var slot = CreateSlotInstance();
                if (slot == null) continue;
                slot.Setup(data, OnSlotClicked);
                _instantiatedSlots.Add(slot);
            }

            foreach (var tile in directTiles)
            {
                if (tile == null) continue;
                if (_activeCategory != "All" && _activeCategory != "Tiles") continue;

                var slot = CreateSlotInstance();
                if (slot == null) continue;
                slot.Setup(tile, tile.name, null, "Tiles", OnSlotClicked);
                _instantiatedSlots.Add(slot);
            }

            for (int i = 0; i < _instantiatedSlots.Count; i++)
            {
                _instantiatedSlots[i].SetHotkeyIndex(i < 9 ? i + 1 : 0);
            }

            if (buildeableTilemap != null && buildeableTilemap.CurrentBuildable != null)
            {
                var curTile = buildeableTilemap.CurrentBuildable.ResolvePrimaryTile();
                var curData = buildeableTilemap.CurrentBuildable.ResolveData();
                HighlightSlotForSelection(curData, curTile);
                RefreshActiveItemDisplay(curData, curTile);
            }
            else if (!_hasUserClearedSelection && _instantiatedSlots.Count > 0)
            {
                OnSlotClicked(_instantiatedSlots[0]);
            }
            else
            {
                HighlightSlot(null);
                RefreshActiveItemDisplay(null, null);
            }
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
            if (clickedSlot == null || buildeableTilemap == null) return;

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

            foreach (var slot in _instantiatedSlots)
            {
                if (slot == null) continue;

                if (data != null && slot.BuildableData == data)
                {
                    matched = slot;
                    break;
                }
                if (tile != null && slot.DirectTile == tile)
                {
                    matched = slot;
                    break;
                }
            }

            HighlightSlot(matched);
        }

        private void RefreshCurrentSelectionDisplay()
        {
            var current = buildeableTilemap != null ? buildeableTilemap.CurrentBuildable : null;
            RefreshActiveItemDisplay(current?.ResolveData(), current?.ResolvePrimaryTile());
        }

        private void RefreshActiveItemDisplay(TmbsBuildableData data, TileBase tile)
        {
            if (buildeableTilemap != null && buildeableTilemap.IsEraseMode)
            {
                if (_titleText != null) _titleText.text = "ERASE MODE";
                if (activeItemNameText != null) activeItemNameText.text = "Mode: Erase";
                bool isPending = buildeableTilemap.RootConfig != null && buildeableTilemap.RootConfig.executionMode == ExecutionMode.Pending;
                if (activeItemDetailsText != null)
                {
                    activeItemDetailsText.text = isPending
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
                if (_titleText != null) _titleText.text = "INSPECT MODE";
                if (activeItemNameText != null) activeItemNameText.text = "Mode: Inspect";
                if (activeItemDetailsText != null) activeItemDetailsText.text = "Select a block from the palette to build.";
                if (activeItemIcon != null)
                {
                    activeItemIcon.enabled = false;
                    activeItemIcon.rectTransform.localEulerAngles = Vector3.zero;
                    activeItemIcon.rectTransform.localScale = Vector3.one;
                }
                return;
            }

            if (_titleText != null) _titleText.text = "BUILD MODE";

            string displayName = data != null ? data.displayName : (tile != null ? tile.name : "Tile");
            string category = data != null ? data.category : "Tiles";
            int work = data != null ? data.workCost : 1;
            int res = data != null ? data.resourceCost : 0;
            Vector2Int sz = data != null ? data.GetSize() : Vector2Int.one;

            if (activeItemNameText != null)
            {
                activeItemNameText.text = $"Building: {displayName}";
            }

            string transformInfo = "";
            int rotation = 0;
            bool mirrored = false;
            if (buildeableTilemap != null)
            {
                var tr = buildeableTilemap.CurrentTransform;
                rotation = tr.Rotation;
                mirrored = tr.Mirrored;
                if (rotation != 0 || mirrored)
                {
                    string mirrorStr = mirrored ? " | Mirrored" : "";
                    transformInfo = $"  |  {rotation * 90} deg{mirrorStr}";
                }
            }

            if (activeItemDetailsText != null)
            {
                activeItemDetailsText.text = $"Size: {sz.x}x{sz.y}  |  Category: {category}  |  Work: {work}  |  Cost: {res}{transformInfo}";
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

        public void ShowToast(string message, bool isWarning = false)
        {
            if (toastPanel == null || toastText == null) return;
            if (!gameObject.activeInHierarchy) return;

            ApplySurfaceStyle(
                toastPanel,
                isWarning ? WarningToastColor : InfoToastColor,
                isWarning ? new Color(0.95f, 0.35f, 0.35f, 1f) : SurfaceOutlineColor);

            toastText.text = message;
            toastPanel.SetActive(true);

            if (_toastCoroutine != null)
                StopCoroutine(_toastCoroutine);

            _toastCoroutine = StartCoroutine(HideToastRoutine(toastDuration));
        }

        private IEnumerator HideToastRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
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
        }

        private static void HookButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(handler);
        }

        private void EnsureToolbarButtons()
        {
            if (activeItemPanel != null)
            {
                Font font = ResolveToolbarFont();
                if (rotateButton == null)
                    rotateButton = FindOrCreateToolbarButton("BtnRotate", "Rotate [R]", font);
                if (mirrorButton == null)
                    mirrorButton = FindOrCreateToolbarButton("BtnMirror", "Mirror [F]", font);
                if (eraseButton == null)
                    eraseButton = FindOrCreateToolbarButton("BtnErase", "Erase [X]", font);
            }

            ConfigureToolbarButton(rotateButton, 0.500f, 0.578f, NeutralButtonColor);
            ConfigureToolbarButton(mirrorButton, 0.582f, 0.660f, NeutralButtonColor);
            ConfigureToolbarButton(eraseButton, 0.664f, 0.744f, EraseNormalColor);
            ConfigureToolbarButton(clearButton, 0.748f, 0.826f, DestructiveIdleColor);
            ConfigureToolbarButton(undoButton, 0.834f, 0.910f, NeutralButtonColor);
            ConfigureToolbarButton(redoButton, 0.914f, 0.990f, NeutralButtonColor);

            RefreshEraseButtonVisual();
        }

        private Font ResolveToolbarFont()
        {
            if (undoButton != null)
            {
                var undoLabel = undoButton.GetComponentInChildren<Text>(true);
                if (undoLabel != null && undoLabel.font != null)
                {
                    return undoLabel.font;
                }
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

        private Button FindOrCreateToolbarButton(string objectName, string labelText, Font font)
        {
            Transform existing = activeItemPanel.transform.Find(objectName);
            if (existing != null)
            {
                var existingBtn = existing.GetComponent<Button>();
                if (existingBtn != null) return existingBtn;
            }

            var btnObj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(activeItemPanel.transform, false);
            btnObj.GetComponent<Image>().raycastTarget = true;

            var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(btnObj.transform, false);
            SetAnchorRect(labelObj.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

            var label = labelObj.GetComponent<Text>();
            label.text = labelText;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = font;
            label.raycastTarget = false;

            return btnObj.GetComponent<Button>();
        }

        private static void ConfigureToolbarButton(Button button, float minX, float maxX, Color bgColor)
        {
            if (button == null) return;
            SetAnchorRect(button.GetComponent<RectTransform>(), new Vector2(minX, 0.16f), new Vector2(maxX, 0.84f));
            ApplyButtonVisual(button, bgColor, SurfaceOutlineColor, 13, 9, 14);
        }

        private void RefreshEraseButtonVisual()
        {
            if (eraseButton == null) return;

            bool active = buildeableTilemap != null && buildeableTilemap.IsEraseMode;
            ApplyButtonVisual(
                eraseButton,
                active ? EraseActiveColor : EraseNormalColor,
                active ? AccentAmberColor : SurfaceOutlineColor,
                13, 9, 14);

            var label = eraseButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = active ? "ERASE ON [X]" : "Erase [X]";
            }
        }

        public void OnRotateClicked()
        {
            if (buildeableTilemap == null) return;

            buildeableTilemap.Rotate();
            RefreshCurrentSelectionDisplay();

            var tr = buildeableTilemap.CurrentTransform;
            string mirrorStr = tr.Mirrored ? " | Mirrored" : "";
            ShowToast($"Rotation: {tr.Rotation * 90} deg{mirrorStr}", false);
        }

        public void OnMirrorClicked()
        {
            if (buildeableTilemap == null) return;

            buildeableTilemap.Mirror();
            RefreshCurrentSelectionDisplay();

            var tr = buildeableTilemap.CurrentTransform;
            ShowToast(tr.Mirrored ? $"Mirrored ({tr.Rotation * 90} deg)" : $"Unmirrored ({tr.Rotation * 90} deg)", false);
        }

        public void OnUndoClicked()
        {
            if (buildeableTilemap == null) return;

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
            if (buildeableTilemap == null) return;

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
            if (buildeableTilemap == null) return;

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

        private void HandlePaletteHotkeys()
        {
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
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }
    }
}
