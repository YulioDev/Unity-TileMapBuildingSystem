using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using TMBS.UI;

namespace TMBS.Editor
{
    public static class TmbsBuildingUIBuilder
    {
        private const string PrefabPath = "Assets/TMBS/UI/TmbsBuildingUI.prefab";
        private const string SlotPrefabPath = "Assets/TMBS/UI/TmbsPaletteSlot.prefab";

        [MenuItem("Tools/TMBS/1. Configure Scene UI", false, 100)]
        public static GameObject CreateOrUpdateSceneUI()
        {
            var buildeable = Object.FindAnyObjectByType<BuildeableTilemap>();
            if (buildeable == null)
            {
                Debug.LogWarning("TMBS: No 'BuildeableTilemap' found in the active scene.");
            }

            EnsureEventSystem();

            // Find or create Canvas
            GameObject canvasObj = GameObject.Find("TMBS_Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("TMBS_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }

            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 64f;

            var raycaster = canvasObj.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Put TmbsBuildingUI directly on canvasObj
            var buildingUI = canvasObj.GetComponent<TmbsBuildingUI>();
            if (buildingUI == null)
            {
                buildingUI = canvasObj.AddComponent<TmbsBuildingUI>();
            }

            // Clear old children if re-generating
            for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(canvasObj.transform.GetChild(i).gameObject);
            }

            // Font
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

            // 1. Top HUD Header
            var topPanel = CreatePanel("TopHUD_Panel", canvasObj.transform,
                new Vector2(0f, 0.915f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.11f, 0.16f, 0.94f));

            // Title
            CreateText("TitleText", topPanel.transform, "TMBS BUILDING SYSTEM",
                18, 11, 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.2f, 0.85f, 1f, 1f),
                new Vector2(0.048f, 0.52f), new Vector2(0.38f, 0.94f), Vector2.zero, defaultFont);

            // Active Item Icon
            var iconObj = new GameObject("ActiveIcon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(topPanel.transform, false);
            var iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.012f, 0.14f);
            iconRt.anchorMax = new Vector2(0.042f, 0.86f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = Vector2.zero;
            var activeIconImg = iconObj.GetComponent<Image>();
            activeIconImg.preserveAspect = true;
            activeIconImg.raycastTarget = false;

            // Active Item Name
            var nameText = CreateText("ActiveItemName", topPanel.transform, "Building: None",
                15, 10, 16, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white,
                new Vector2(0.048f, 0.08f), new Vector2(0.26f, 0.50f), Vector2.zero, defaultFont);

            // Active Item Details
            var detailsText = CreateText("ActiveItemDetails", topPanel.transform, "Select a buildable from the palette below",
                14, 9, 15, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.75f, 0.8f, 0.88f, 0.95f),
                new Vector2(0.265f, 0.08f), new Vector2(0.565f, 0.50f), Vector2.zero, defaultFont);

            // Pending Construction Orders Badge
            var pendingPanel = CreatePanel("PendingOrders_Panel", topPanel.transform,
                new Vector2(0.39f, 0.54f), new Vector2(0.565f, 0.92f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.25f, 0.2f, 0.05f, 0.9f));
            var pendingCountText = CreateText("PendingOrders_Text", pendingPanel.transform, "Pending orders: 0",
                14, 9, 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, defaultFont);
            pendingPanel.SetActive(false);

            // Action Buttons (Erase, Undo, Redo, Clear)
            var eraseBtn = CreateButton("BtnErase", topPanel.transform, "Erase [X]",
                new Vector2(0.58f, 0.2f), new Vector2(0.67f, 0.8f), new Color(0.45f, 0.22f, 0.16f, 1f), defaultFont);

            var undoBtn = CreateButton("BtnUndo", topPanel.transform, "Undo [Ctrl+Z]",
                new Vector2(0.68f, 0.2f), new Vector2(0.77f, 0.8f), new Color(0.18f, 0.32f, 0.48f, 1f), defaultFont);

            var redoBtn = CreateButton("BtnRedo", topPanel.transform, "Redo [Ctrl+Y]",
                new Vector2(0.78f, 0.2f), new Vector2(0.87f, 0.8f), new Color(0.18f, 0.32f, 0.48f, 1f), defaultFont);

            var clearBtn = CreateButton("BtnClear", topPanel.transform, "Clear [Esc]",
                new Vector2(0.88f, 0.2f), new Vector2(0.98f, 0.8f), new Color(0.48f, 0.18f, 0.24f, 1f), defaultFont);

            // 2. Bottom Palette Panel
            var bottomPanel = CreatePanel("BottomPalette_Panel", canvasObj.transform,
                new Vector2(0.12f, 0.015f), new Vector2(0.88f, 0.175f), new Vector2(0.5f, 0f),
                Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.11f, 0.16f, 0.95f));

            CreateText("PaletteTitle", bottomPanel.transform, "BUILD CATALOG  (Hotkeys: 1-9)",
                14, 10, 15, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.4f, 0.8f, 1f, 0.9f),
                new Vector2(0.02f, 0.80f), new Vector2(0.98f, 0.97f), Vector2.zero, defaultFont);

            // Category Tabs container with HorizontalLayoutGroup
            var tabsContainer = new GameObject("CategoryTabs_Container", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabsContainer.transform.SetParent(bottomPanel.transform, false);
            var tabsRt = tabsContainer.GetComponent<RectTransform>();
            tabsRt.anchorMin = new Vector2(0.02f, 0.60f);
            tabsRt.anchorMax = new Vector2(0.98f, 0.79f);
            tabsRt.anchoredPosition = Vector2.zero;
            tabsRt.sizeDelta = Vector2.zero;

            var tabsLayout = tabsContainer.GetComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 8f;
            tabsLayout.childAlignment = TextAnchor.MiddleLeft;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandHeight = true;
            tabsLayout.childForceExpandWidth = false;

            // Slots container with HorizontalLayoutGroup
            var slotsContainer = new GameObject("SlotsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            slotsContainer.transform.SetParent(bottomPanel.transform, false);
            var slotsRt = slotsContainer.GetComponent<RectTransform>();
            slotsRt.anchorMin = new Vector2(0.02f, 0.05f);
            slotsRt.anchorMax = new Vector2(0.98f, 0.57f);
            slotsRt.anchoredPosition = Vector2.zero;
            slotsRt.sizeDelta = Vector2.zero;

            var layout = slotsContainer.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            // 3. Validation Toast Panel
            var toastPanel = CreatePanel("ValidationToast_Panel", canvasObj.transform,
                new Vector2(0.28f, 0.19f), new Vector2(0.72f, 0.245f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.65f, 0.15f, 0.18f, 0.95f));

            var toastText = CreateText("ToastMessage", toastPanel.transform, "Validation notice",
                16, 10, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, defaultFont);

            toastPanel.SetActive(false);

            // Collect buildables in project (only valid ones)
            var foundBuildables = new List<TmbsBuildableData>();
            var guids = AssetDatabase.FindAssets("t:TmbsBuildableData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TmbsBuildableData>(path);
                if (asset != null && asset.tile != null && !foundBuildables.Contains(asset))
                {
                    foundBuildables.Add(asset);
                }
            }

            // Load Slot Prefab if available
            var slotPrefab = AssetDatabase.LoadAssetAtPath<TmbsPaletteSlot>(SlotPrefabPath);

            // Wire serialized fields via SerializedObject
            var so = new SerializedObject(buildingUI);
            so.FindProperty("buildeableTilemap").objectReferenceValue = buildeable;
            so.FindProperty("paletteContainer").objectReferenceValue = slotsContainer.transform;
            so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            so.FindProperty("categoryTabsContainer").objectReferenceValue = tabsContainer.transform;
            so.FindProperty("activeItemIcon").objectReferenceValue = activeIconImg;
            so.FindProperty("activeItemNameText").objectReferenceValue = nameText;
            so.FindProperty("activeItemDetailsText").objectReferenceValue = detailsText;
            so.FindProperty("activeItemPanel").objectReferenceValue = topPanel;
            so.FindProperty("undoButton").objectReferenceValue = undoBtn;
            so.FindProperty("redoButton").objectReferenceValue = redoBtn;
            so.FindProperty("clearButton").objectReferenceValue = clearBtn;
            so.FindProperty("eraseButton").objectReferenceValue = eraseBtn;
            so.FindProperty("toastPanel").objectReferenceValue = toastPanel;
            so.FindProperty("toastText").objectReferenceValue = toastText;
            so.FindProperty("pendingPanel").objectReferenceValue = pendingPanel;
            so.FindProperty("pendingCountText").objectReferenceValue = pendingCountText;

            var buildablesProp = so.FindProperty("buildables");
            buildablesProp.ClearArray();
            for (int i = 0; i < foundBuildables.Count; i++)
            {
                buildablesProp.InsertArrayElementAtIndex(i);
                buildablesProp.GetArrayElementAtIndex(i).objectReferenceValue = foundBuildables[i];
            }

            so.ApplyModifiedProperties();

            // Mark Scene Dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"TMBS: Scene UI configured with {foundBuildables.Count} available buildables.");

            return canvasObj;
        }

        [MenuItem("Tools/TMBS/2. Save UI as Prefab", false, 101)]
        public static void SaveAsPrefab()
        {
            var canvasObj = GameObject.Find("TMBS_Canvas");
            if (canvasObj == null)
            {
                canvasObj = CreateOrUpdateSceneUI();
            }

            if (!AssetDatabase.IsValidFolder("Assets/TMBS/UI"))
            {
                AssetDatabase.CreateFolder("Assets/TMBS", "UI");
            }

            // Create or update prefab
            var buildingUI = canvasObj.GetComponent<TmbsBuildingUI>();
            var cachedBuildeable = buildingUI != null ? buildingUI.BuildeableTilemap : null;

            // In prefab asset, buildeableTilemap should be null so it auto-detects dynamically at runtime in any scene
            if (buildingUI != null)
            {
                var so = new SerializedObject(buildingUI);
                so.FindProperty("buildeableTilemap").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasObj, PrefabPath, InteractionMode.UserAction);

            // Re-assign in scene instance
            if (buildingUI != null && cachedBuildeable != null)
            {
                var so = new SerializedObject(buildingUI);
                so.FindProperty("buildeableTilemap").objectReferenceValue = cachedBuildeable;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"TMBS: UI prefab saved at: {PrefabPath}");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null && EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return obj;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, int minSize, int maxSize, FontStyle style, TextAnchor align, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Font font)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offset;
            rt.offsetMax = Vector2.zero;

            var txt = obj.GetComponent<Text>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = minSize;
            txt.resizeTextMaxSize = maxSize;
            txt.fontStyle = style;
            txt.alignment = align;
            txt.color = color;
            txt.font = font;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Color btnColor, Font font)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(4, 4);
            rt.offsetMax = new Vector2(-4, -4);

            var img = obj.GetComponent<Image>();
            img.color = btnColor;
            img.raycastTarget = true;

            var btn = obj.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = btnColor * 1.3f;
            colors.pressedColor = btnColor * 0.7f;
            btn.colors = colors;

            CreateText("Label", obj.transform, label, 14, 9, 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, font);
            return btn;
        }
    }
}
