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

        // Runtime state
        private readonly List<TmbsPaletteSlot> _instantiatedSlots = new List<TmbsPaletteSlot>();
        private readonly List<string> _categories = new List<string> { "All" };
        private readonly List<Button> _instantiatedCategoryButtons = new List<Button>();
        private string _activeCategory = "All";
        private Coroutine _toastCoroutine;
        private bool _hasUserClearedSelection = false;
        private Font _defaultFont;

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

        /// <summary>
        /// Populates the category filter tabs above the palette.
        /// </summary>
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

            if (_defaultFont == null)
            {
                _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_defaultFont == null)
                {
                    _defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 11);
                }
            }

            foreach (var cat in _categories)
            {
                string categoryName = cat;

                var tabObj = new GameObject($"Tab_{categoryName}", typeof(RectTransform), typeof(Image), typeof(Button));
                tabObj.transform.SetParent(categoryTabsContainer, false);

                var rt = tabObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(85f, 24f);

                var btn = tabObj.GetComponent<Button>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(0.25f, 0.65f, 0.95f, 1f);
                colors.pressedColor = new Color(0.12f, 0.4f, 0.65f, 1f);
                btn.colors = colors;

                btn.onClick.AddListener(() => SetCategoryFilter(categoryName));

                var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObj.transform.SetParent(tabObj.transform, false);
                var labelRt = labelObj.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.sizeDelta = Vector2.zero;

                var txt = labelObj.GetComponent<Text>();
                txt.text = categoryName;
                txt.fontSize = 11;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.font = _defaultFont;
                txt.raycastTarget = false;

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
                    img.color = isActive
                        ? new Color(0.18f, 0.55f, 0.85f, 1f)
                        : new Color(0.14f, 0.18f, 0.24f, 0.85f);
                }

                if (label != null)
                {
                    label.fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
                    label.color = isActive ? Color.white : new Color(0.75f, 0.85f, 0.95f, 0.9f);
                }
            }
        }

        public void SetCategoryFilter(string category)
        {
            _activeCategory = category;
            RefreshCategoryTabVisuals();
            BuildPalette();
        }

        /// <summary>
        /// Rebuilds the palette slots for the current buildables and active category filter.
        /// </summary>
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

        private void RefreshActiveItemDisplay(TmbsBuildableData data, TileBase tile)
        {
            if (data == null && tile == null)
            {
                if (activeItemNameText != null) activeItemNameText.text = "Mode: Inspect";
                if (activeItemDetailsText != null) activeItemDetailsText.text = "Select a block from the palette to build.";
                if (activeItemIcon != null) activeItemIcon.enabled = false;
                return;
            }

            string displayName = data != null ? data.displayName : (tile != null ? tile.name : "Tile");
            string category = data != null ? data.category : "Tiles";
            int work = data != null ? data.workCost : 1;
            int res = data != null ? data.resourceCost : 0;

            if (activeItemNameText != null)
            {
                activeItemNameText.text = $"Building: {displayName}";
            }

            string transformInfo = "";
            if (buildeableTilemap != null)
            {
                var tr = buildeableTilemap.CurrentTransform;
                if (tr.Rotation != 0 || tr.Mirrored)
                {
                    string mirrorStr = tr.Mirrored ? " | Mirrored" : "";
                    transformInfo = $"  |  {tr.Rotation * 90} deg{mirrorStr}";
                }
            }

            if (activeItemDetailsText != null)
            {
                activeItemDetailsText.text = $"Category: {category}  |  Work: {work}  |  Cost: {res}{transformInfo}";
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
            ShowToast("Selection cleared.");
        }

        private void HandlePaletteHotkeys()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
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
