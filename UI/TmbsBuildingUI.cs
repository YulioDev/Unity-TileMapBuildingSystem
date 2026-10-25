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
    /// Master UI controller for TMBS (TileMap Building System).
    /// Provides building palette, dynamic category tabs, HUD with active item details,
    /// Undo/Redo controls, pending build orders badge, and validation feedback toasts.
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

        [Tooltip("If true and buildables list is empty, scans the project for existing TmbsBuildableData at runtime.")]
        [SerializeField] private bool autoPopulateBuildables = true;

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
        [SerializeField] private Button undoButton;
        [SerializeField] private Button redoButton;
        [SerializeField] private Button clearButton;

        [Header("Validation Feedback (Toast)")]
        [SerializeField] private GameObject toastPanel;
        [SerializeField] private Text toastText;
        [SerializeField] private float toastDuration = 2.5f;

        [Header("Pending Construction Status")]
        [SerializeField] private GameObject pendingPanel;
        [SerializeField] private Text pendingCountText;

        [Header("Shortcuts")]
        [SerializeField] private bool enableKeyboardShortcuts = true;

        // Runtime state
        private readonly List<TmbsPaletteSlot> _instantiatedSlots = new List<TmbsPaletteSlot>();
        private readonly List<string> _categories = new List<string> { "Todos" };
        private readonly List<Button> _instantiatedCategoryButtons = new List<Button>();
        private string _activeCategory = "Todos";
        private TmbsPaletteSlot _currentSelectedSlot;
        private Coroutine _toastCoroutine;
        private bool _hasUserClearedSelection = false;

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

            if (paletteContainer == null || activeItemNameText == null)
            {
                BuildProceduralLayoutIfNeeded();
            }

            HookButtons();
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

            if (autoPopulateBuildables && buildables.Count == 0 && directTiles.Count == 0)
            {
                DiscoverDefaultBuildables();
            }

            BuildCategoryTabs();
            BuildPalette();
            RefreshActiveItemDisplay(null, null);
            UpdatePendingCount();
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
                HandleKeyboardShortcuts();
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

        #region Event Subscriptions

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
        }

        private void OnTmbsValidationFailed(ValidationFailedEvent evt)
        {
            if (buildeableTilemap != null && evt.InstanceId != buildeableTilemap.InstanceId)
                return;

            if (evt.Failure == ValidationFailure.None)
                return;

            string message;
            switch (evt.Failure)
            {
                case ValidationFailure.MissingSelection:
                    message = "Selecciona un bloque en la paleta antes de construir.";
                    break;
                case ValidationFailure.Occupied:
                    message = "Espacio ocupado. No se puede construir aquí.";
                    break;
                case ValidationFailure.OutOfBounds:
                    message = "Fuera de los límites de construcción.";
                    break;
                default:
                    message = "Ubicación de construcción inválida.";
                    break;
            }

            ShowToast(message);
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

        #endregion

        #region Category Tabs

        /// <summary>
        /// Populates the category tabs above the palette and binds category filter click handlers.
        /// </summary>
        public void BuildCategoryTabs()
        {
            // Collect categories
            _categories.Clear();
            _categories.Add("Todos");

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

            // Clear existing category buttons
            for (int i = categoryTabsContainer.childCount - 1; i >= 0; i--)
            {
                var child = categoryTabsContainer.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
            _instantiatedCategoryButtons.Clear();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 11);

            foreach (var cat in _categories)
            {
                string categoryName = cat;
                bool isActive = categoryName == _activeCategory;

                var tabObj = new GameObject($"Tab_{categoryName}", typeof(RectTransform), typeof(Image), typeof(Button));
                tabObj.transform.SetParent(categoryTabsContainer, false);

                var rt = tabObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(85f, 24f);

                var img = tabObj.GetComponent<Image>();
                img.color = isActive ? new Color(0.18f, 0.55f, 0.85f, 1f) : new Color(0.14f, 0.18f, 0.24f, 0.85f);

                var btn = tabObj.GetComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.25f, 0.65f, 0.95f, 1f);
                colors.pressedColor = new Color(0.12f, 0.4f, 0.65f, 1f);
                btn.colors = colors;

                btn.onClick.AddListener(() => SetCategoryFilter(categoryName));

                // Label
                var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObj.transform.SetParent(tabObj.transform, false);
                var labelRt = labelObj.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.sizeDelta = Vector2.zero;

                var txt = labelObj.GetComponent<Text>();
                txt.text = categoryName;
                txt.fontSize = 11;
                txt.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = isActive ? Color.white : new Color(0.75f, 0.85f, 0.95f, 0.9f);
                txt.font = defaultFont;
                txt.raycastTarget = false;

                _instantiatedCategoryButtons.Add(btn);
            }
        }

        public void SetCategoryFilter(string category)
        {
            _activeCategory = category;
            BuildCategoryTabs();
            BuildPalette();
        }

        #endregion

        #region Palette Management

        /// <summary>
        /// Populates or refreshes the palette slots based on current buildables and category filter.
        /// </summary>
        public void BuildPalette()
        {
            if (paletteContainer == null)
                return;

            // Clear existing slots safely in play or edit mode
            for (int i = paletteContainer.childCount - 1; i >= 0; i--)
            {
                var child = paletteContainer.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
            _instantiatedSlots.Clear();

            // Create slot for each buildable
            foreach (var data in buildables)
            {
                if (data == null) continue;
                if (_activeCategory != "Todos" && data.category != _activeCategory) continue;

                var slot = CreateSlotInstance();
                slot.Setup(data, OnSlotClicked);
                _instantiatedSlots.Add(slot);
            }

            // Create slot for direct tiles
            foreach (var tile in directTiles)
            {
                if (tile == null) continue;
                if (_activeCategory != "Todos" && _activeCategory != "Tiles") continue;

                var slot = CreateSlotInstance();
                slot.Setup(tile, tile.name, null, "Tiles", OnSlotClicked);
                _instantiatedSlots.Add(slot);
            }

            // Selection state management
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
            if (slotPrefab != null)
            {
                return Instantiate(slotPrefab, paletteContainer);
            }

            // Fallback: create procedural slot GameObject
            var slotObj = new GameObject("PaletteSlot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(TmbsPaletteSlot));
            slotObj.transform.SetParent(paletteContainer, false);

            var rt = slotObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80, 80);

            var bgImage = slotObj.GetComponent<Image>();
            bgImage.color = new Color(0.12f, 0.15f, 0.2f, 0.85f);

            var btn = slotObj.GetComponent<Button>();

            // Border highlight
            var borderObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(slotObj.transform, false);
            var borderRt = borderObj.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = Vector2.zero;
            var borderImg = borderObj.GetComponent<Image>();
            borderImg.color = new Color(0.2f, 0.25f, 0.35f, 0.8f);
            borderImg.raycastTarget = false;

            // Icon Image
            var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(slotObj.transform, false);
            var iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.25f);
            iconRt.anchorMax = new Vector2(0.9f, 0.95f);
            iconRt.sizeDelta = Vector2.zero;
            var iconImg = iconObj.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Label Text
            var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(slotObj.transform, false);
            var labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0.25f);
            labelRt.sizeDelta = Vector2.zero;
            var lbl = labelObj.GetComponent<Text>();
            lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (lbl.font == null) lbl.font = Font.CreateDynamicFontFromOSFont("Arial", 11);
            lbl.fontSize = 10;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = Color.white;
            lbl.raycastTarget = false;

            var comp = slotObj.GetComponent<TmbsPaletteSlot>();
            comp.SetInternalReferences(iconImg, lbl, borderImg, btn);
            return comp;
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
        }

        private void HighlightSlot(TmbsPaletteSlot targetSlot)
        {
            _currentSelectedSlot = targetSlot;
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

        #endregion

        #region HUD & Notifications

        private void RefreshActiveItemDisplay(TmbsBuildableData data, TileBase tile)
        {
            if (data == null && tile == null)
            {
                if (activeItemNameText != null) activeItemNameText.text = "Modo: Navegación (Inspección)";
                if (activeItemDetailsText != null) activeItemDetailsText.text = "Haz clic en un bloque de la paleta inferior para construir.";
                if (activeItemIcon != null) activeItemIcon.enabled = false;
                return;
            }

            string displayName = data != null ? data.displayName : (tile != null ? tile.name : "Tile");
            string category = data != null ? data.category : "Tiles";
            int work = data != null ? data.workCost : 1;
            int res = data != null ? data.resourceCost : 0;

            if (activeItemNameText != null)
            {
                activeItemNameText.text = $"Construyendo: {displayName}";
            }

            if (activeItemDetailsText != null)
            {
                activeItemDetailsText.text = $"Categoría: {category}  |  Trabajo: {work}  |  Costo: {res}";
            }

            if (activeItemIcon != null)
            {
                Sprite icon = TmbsPaletteSlot.ResolveSprite(data, tile);
                activeItemIcon.sprite = icon;
                activeItemIcon.enabled = icon != null;
            }
        }

        public void ShowToast(string message)
        {
            if (toastPanel == null || toastText == null) return;
            if (!gameObject.activeInHierarchy) return;

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
                    pendingCountText.text = $"⛏ Órdenes pendientes: {count}";

                if (pendingPanel != null)
                    pendingPanel.SetActive(count > 0);
            }
            else
            {
                if (pendingPanel != null)
                    pendingPanel.SetActive(false);
            }
        }

        #endregion

        #region Actions & Controls

        private void HookButtons()
        {
            if (undoButton != null)
            {
                undoButton.onClick.RemoveAllListeners();
                undoButton.onClick.AddListener(OnUndoClicked);
            }

            if (redoButton != null)
            {
                redoButton.onClick.RemoveAllListeners();
                redoButton.onClick.AddListener(OnRedoClicked);
            }

            if (clearButton != null)
            {
                clearButton.onClick.RemoveAllListeners();
                clearButton.onClick.AddListener(OnClearClicked);
            }
        }

        public void OnUndoClicked()
        {
            if (buildeableTilemap == null) return;

            if (buildeableTilemap.RootConfig?.history != null && !buildeableTilemap.RootConfig.history.enableUndoRedo)
            {
                ShowToast("Deshacer/Rehacer desactivado en configuración.");
                return;
            }

            bool success = buildeableTilemap.Undo();
            if (success)
            {
                ShowToast("↺ Deshacer completado.");
            }
            else
            {
                ShowToast("Nada que deshacer.");
            }
        }

        public void OnRedoClicked()
        {
            if (buildeableTilemap == null) return;

            if (buildeableTilemap.RootConfig?.history != null && !buildeableTilemap.RootConfig.history.enableUndoRedo)
            {
                ShowToast("Deshacer/Rehacer desactivado en configuración.");
                return;
            }

            bool success = buildeableTilemap.Redo();
            if (success)
            {
                ShowToast("↻ Rehacer completado.");
            }
            else
            {
                ShowToast("Nada que rehacer.");
            }
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
            ShowToast("Selección desmarcada (Modo navegación).");
        }

        private void HandleKeyboardShortcuts()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.zKey.wasPressedThisFrame)
                {
                    OnUndoClicked();
                    return;
                }
                if (keyboard.yKey.wasPressedThisFrame)
                {
                    OnRedoClicked();
                    return;
                }
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    OnClearClicked();
                    return;
                }

                // Digit keys 1-9
                Key[] digitKeys = {
                    Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
                    Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
                };
                Key[] numpadKeys = {
                    Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5,
                    Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
                };

                for (int i = 0; i < 9 && i < _instantiatedSlots.Count; i++)
                {
                    if (keyboard[digitKeys[i]].wasPressedThisFrame || keyboard[numpadKeys[i]].wasPressedThisFrame)
                    {
                        OnSlotClicked(_instantiatedSlots[i]);
                        return;
                    }
                }
                return;
            }
#endif
            // Legacy Input fallback
            if (Input.GetKeyDown(KeyCode.Z))
            {
                OnUndoClicked();
            }
            else if (Input.GetKeyDown(KeyCode.Y))
            {
                OnRedoClicked();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnClearClicked();
            }
            else
            {
                for (int i = 0; i < 9 && i < _instantiatedSlots.Count; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    {
                        OnSlotClicked(_instantiatedSlots[i]);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Helpers & Auto-Discovery

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

        private void DiscoverDefaultBuildables()
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:TmbsBuildableData");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TmbsBuildableData>(path);
                if (asset != null && (asset.tile != null || asset.isBlueprint) && !buildables.Contains(asset))
                {
                    buildables.Add(asset);
                }
            }
#else
            var loaded = Resources.FindObjectsOfTypeAll<TmbsBuildableData>();
            if (loaded != null)
            {
                foreach (var asset in loaded)
                {
                    if (asset != null && (asset.tile != null || asset.isBlueprint) && !buildables.Contains(asset))
                    {
                        buildables.Add(asset);
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Procedurally constructs a complete UI hierarchy if not loaded from a prefab.
        /// </summary>
        private void BuildProceduralLayoutIfNeeded()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("TMBS_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                transform.SetParent(canvasObj.transform, false);
            }

            var rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
                rootRt = gameObject.AddComponent<RectTransform>();

            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

            // 1. Top HUD Panel
            if (activeItemPanel == null)
            {
                activeItemPanel = CreateUiPanel("TopHUD_Panel", rootRt,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 75f),
                    new Color(0.08f, 0.11f, 0.16f, 0.94f));

                CreateUiText("Title", activeItemPanel.transform, "SISTEMA DE CONSTRUCCIÓN TMBS",
                    16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.2f, 0.85f, 1f, 1f),
                    new Vector2(0.02f, 0.52f), new Vector2(0.35f, 0.95f), Vector2.zero);

                // Active icon
                var iconObj = new GameObject("ActiveIcon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(activeItemPanel.transform, false);
                var iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.02f, 0.1f);
                iconRt.anchorMax = new Vector2(0.02f, 0.1f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(14f, 14f);
                iconRt.sizeDelta = new Vector2(28f, 28f);
                activeItemIcon = iconObj.GetComponent<Image>();
                activeItemIcon.preserveAspect = true;
                activeItemIcon.raycastTarget = false;

                activeItemNameText = CreateUiText("ItemName", activeItemPanel.transform, "Construyendo: Ninguno",
                    13, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white,
                    new Vector2(0.045f, 0.1f), new Vector2(0.35f, 0.5f), Vector2.zero);

                activeItemDetailsText = CreateUiText("ItemDetails", activeItemPanel.transform, "Selecciona un bloque para comenzar",
                    12, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.75f, 0.8f, 0.88f, 0.95f),
                    new Vector2(0.32f, 0.1f), new Vector2(0.65f, 0.5f), Vector2.zero);

                // Pending construction status badge
                pendingPanel = CreateUiPanel("PendingOrders_Panel", activeItemPanel.transform,
                    new Vector2(0.5f, 0.52f), new Vector2(0.66f, 0.95f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, new Color(0.25f, 0.2f, 0.05f, 0.9f));
                pendingCountText = CreateUiText("PendingCount", pendingPanel.transform, "⛏ Órdenes: 0",
                    12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f, 1f),
                    Vector2.zero, Vector2.one, Vector2.zero);
                pendingPanel.SetActive(false);

                // Header buttons
                undoButton = CreateUiButton("BtnUndo", activeItemPanel.transform, "↺ Deshacer [Z]",
                    new Vector2(0.68f, 0.2f), new Vector2(0.77f, 0.8f), new Color(0.18f, 0.32f, 0.48f, 1f));

                redoButton = CreateUiButton("BtnRedo", activeItemPanel.transform, "↻ Rehacer [Y]",
                    new Vector2(0.78f, 0.2f), new Vector2(0.87f, 0.8f), new Color(0.18f, 0.32f, 0.48f, 1f));

                clearButton = CreateUiButton("BtnClear", activeItemPanel.transform, "✖ Deseleccionar [Esc]",
                    new Vector2(0.88f, 0.2f), new Vector2(0.98f, 0.8f), new Color(0.48f, 0.18f, 0.24f, 1f));
            }

            // 2. Bottom Palette Panel
            if (paletteContainer == null)
            {
                var bottomPanel = CreateUiPanel("BottomPalette_Panel", rootRt,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(900f, 140f),
                    new Color(0.08f, 0.11f, 0.16f, 0.95f));

                CreateUiText("PaletteHeader", bottomPanel.transform, "CATÁLOGO DE CONSTRUCCIÓN  (Acceso rápido: Teclas 1 a 9)",
                    12, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.4f, 0.8f, 1f, 0.9f),
                    new Vector2(0.02f, 0.82f), new Vector2(0.98f, 0.98f), Vector2.zero);

                // Category tabs container
                var tabsObj = new GameObject("CategoryTabs_Container", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                tabsObj.transform.SetParent(bottomPanel.transform, false);
                var tabsRt = tabsObj.GetComponent<RectTransform>();
                tabsRt.anchorMin = new Vector2(0.02f, 0.64f);
                tabsRt.anchorMax = new Vector2(0.98f, 0.82f);
                tabsRt.sizeDelta = Vector2.zero;
                var tabsLayout = tabsObj.GetComponent<HorizontalLayoutGroup>();
                tabsLayout.spacing = 8;
                tabsLayout.childAlignment = TextAnchor.MiddleLeft;
                tabsLayout.childForceExpandHeight = true;
                tabsLayout.childForceExpandWidth = false;
                categoryTabsContainer = tabsObj.transform;

                // Slots container
                var slotsContainerObj = new GameObject("SlotsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                slotsContainerObj.transform.SetParent(bottomPanel.transform, false);
                var slotsRt = slotsContainerObj.GetComponent<RectTransform>();
                slotsRt.anchorMin = new Vector2(0.02f, 0.06f);
                slotsRt.anchorMax = new Vector2(0.98f, 0.62f);
                slotsRt.sizeDelta = Vector2.zero;

                var layout = slotsContainerObj.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 12;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childForceExpandHeight = true;
                layout.childForceExpandWidth = false;

                paletteContainer = slotsContainerObj.transform;
            }

            // 3. Validation Toast Panel
            if (toastPanel == null)
            {
                toastPanel = CreateUiPanel("ValidationToast_Panel", rootRt,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(520f, 50f),
                    new Color(0.65f, 0.15f, 0.18f, 0.95f));

                toastText = CreateUiText("ToastMessage", toastPanel.transform, "Aviso de validación",
                    14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
                    Vector2.zero, Vector2.one, Vector2.zero);

                toastPanel.SetActive(false);
            }
        }

        private GameObject CreateUiPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = panel.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return panel;
        }

        private Text CreateUiText(string name, Transform parent, string content, int fontSize, FontStyle style, TextAnchor align, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset)
        {
            var textObj = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(parent, false);
            var rt = textObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offset;
            rt.offsetMax = Vector2.zero;

            var txt = textObj.GetComponent<Text>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = align;
            txt.color = color;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.raycastTarget = false;
            return txt;
        }

        private Button CreateUiButton(string name, Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, Color btnColor)
        {
            var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(4, 4);
            rt.offsetMax = new Vector2(-4, -4);

            var img = btnObj.GetComponent<Image>();
            img.color = btnColor;
            img.raycastTarget = true;

            var btn = btnObj.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = btnColor * 1.25f;
            colors.pressedColor = btnColor * 0.75f;
            btn.colors = colors;

            CreateUiText("Label", btnObj.transform, text, 12, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero);
            return btn;
        }

        #endregion
    }
}
