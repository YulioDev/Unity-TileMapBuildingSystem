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

        private static readonly Color SurfacePanelColor = new Color(0.10f, 0.11f, 0.14f, 0.92f);
        private static readonly Color SurfaceOutlineColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color NeutralButtonColor = new Color(0.17f, 0.19f, 0.23f, 0.96f);
        private static readonly Color DestructiveIdleColor = new Color(0.28f, 0.16f, 0.17f, 0.96f);
        private static readonly Color EraseNormalColor = new Color(0.26f, 0.18f, 0.16f, 0.96f);
        private static readonly Color AccentAmberColor = new Color(0.96f, 0.65f, 0.14f, 1f);
        private static readonly Color InfoToastColor = new Color(0.14f, 0.16f, 0.20f, 0.96f);
        private static readonly Color PendingPanelColor = new Color(0.20f, 0.16f, 0.07f, 0.96f);

        [MenuItem("Tools/TMBS/1. Configure Scene UI", false, 100)]
        public static GameObject CreateOrUpdateSceneUI()
        {
            var buildeable = Object.FindAnyObjectByType<BuildeableTilemap>();
            if (buildeable == null)
            {
                Debug.LogWarning("TMBS: No 'BuildeableTilemap' found in the active scene.");
            }

            EnsureEventSystem();

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

            var buildingUI = canvasObj.GetComponent<TmbsBuildingUI>();
            if (buildingUI == null)
            {
                buildingUI = canvasObj.AddComponent<TmbsBuildingUI>();
            }

            for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(canvasObj.transform.GetChild(i).gameObject);
            }

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

            var topPanel = CreatePanel("TopHUD_Panel", canvasObj.transform,
                new Vector2(0.015f, 0.905f), new Vector2(0.985f, 0.985f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero, SurfacePanelColor, SurfaceOutlineColor);

            CreateText("TitleText", topPanel.transform, "TMBS BUILDING SYSTEM",
                16, 10, 18, FontStyle.Bold, TextAnchor.MiddleLeft, AccentAmberColor,
                new Vector2(0.06f, 0.54f), new Vector2(0.32f, 0.92f), Vector2.zero, defaultFont);

            var iconObj = new GameObject("ActiveIcon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(topPanel.transform, false);
            var iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.01f, 0.12f);
            iconRt.anchorMax = new Vector2(0.052f, 0.88f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = Vector2.zero;
            var activeIconImg = iconObj.GetComponent<Image>();
            activeIconImg.preserveAspect = true;
            activeIconImg.raycastTarget = false;

            var nameText = CreateText("ActiveItemName", topPanel.transform, "Building: None",
                15, 10, 16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.95f, 0.96f, 0.98f, 1f),
                new Vector2(0.06f, 0.08f), new Vector2(0.24f, 0.52f), Vector2.zero, defaultFont);

            var detailsText = CreateText("ActiveItemDetails", topPanel.transform, "Select a buildable from the palette below",
                13, 9, 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.75f, 0.78f, 0.83f, 1f),
                new Vector2(0.245f, 0.08f), new Vector2(0.485f, 0.52f), Vector2.zero, defaultFont);

            var pendingPanel = CreatePanel("PendingOrders_Panel", topPanel.transform,
                new Vector2(0.33f, 0.54f), new Vector2(0.485f, 0.92f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, PendingPanelColor, AccentAmberColor);
            var pendingCountText = CreateText("PendingOrders_Text", pendingPanel.transform, "Pending orders: 0",
                14, 9, 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, defaultFont);
            pendingPanel.SetActive(false);

            var rotateBtn = CreateButton("BtnRotate", topPanel.transform, "Rotate [R]",
                new Vector2(0.500f, 0.16f), new Vector2(0.578f, 0.84f), NeutralButtonColor, defaultFont);

            var mirrorBtn = CreateButton("BtnMirror", topPanel.transform, "Mirror [F]",
                new Vector2(0.582f, 0.16f), new Vector2(0.660f, 0.84f), NeutralButtonColor, defaultFont);

            var eraseBtn = CreateButton("BtnErase", topPanel.transform, "Erase [X]",
                new Vector2(0.664f, 0.16f), new Vector2(0.744f, 0.84f), EraseNormalColor, defaultFont);

            var clearBtn = CreateButton("BtnClear", topPanel.transform, "Clear [Esc]",
                new Vector2(0.748f, 0.16f), new Vector2(0.826f, 0.84f), DestructiveIdleColor, defaultFont);

            var undoBtn = CreateButton("BtnUndo", topPanel.transform, "Undo [Ctrl+Z]",
                new Vector2(0.834f, 0.16f), new Vector2(0.910f, 0.84f), NeutralButtonColor, defaultFont);

            var redoBtn = CreateButton("BtnRedo", topPanel.transform, "Redo [Ctrl+Y]",
                new Vector2(0.914f, 0.16f), new Vector2(0.990f, 0.84f), NeutralButtonColor, defaultFont);

            var bottomPanel = CreatePanel("BottomPalette_Panel", canvasObj.transform,
                new Vector2(0.12f, 0.015f), new Vector2(0.88f, 0.175f), new Vector2(0.5f, 0f),
                Vector2.zero, Vector2.zero, SurfacePanelColor, SurfaceOutlineColor);

            CreateText("PaletteTitle", bottomPanel.transform, "BUILD CATALOG  (Hotkeys: 1-9)",
                14, 10, 15, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.88f, 0.90f, 0.94f, 1f),
                new Vector2(0.02f, 0.80f), new Vector2(0.98f, 0.97f), Vector2.zero, defaultFont);

            var tabsContainer = CreateMaskedContainer("CategoryTabs_Container", bottomPanel.transform,
                new Vector2(0.02f, 0.60f), new Vector2(0.98f, 0.79f), 8f, true);

            var slotsContainer = CreateMaskedContainer("SlotsContainer", bottomPanel.transform,
                new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.57f), 12f, false);

            var toastPanel = CreatePanel("ValidationToast_Panel", canvasObj.transform,
                new Vector2(0.28f, 0.19f), new Vector2(0.72f, 0.245f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, InfoToastColor, SurfaceOutlineColor);

            var toastText = CreateText("ToastMessage", toastPanel.transform, "Validation notice",
                16, 10, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, defaultFont);

            toastPanel.SetActive(false);

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

            var slotPrefab = AssetDatabase.LoadAssetAtPath<TmbsPaletteSlot>(SlotPrefabPath);

            var so = new SerializedObject(buildingUI);
            so.FindProperty("buildeableTilemap").objectReferenceValue = buildeable;
            so.FindProperty("paletteContainer").objectReferenceValue = slotsContainer.transform;
            so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            so.FindProperty("categoryTabsContainer").objectReferenceValue = tabsContainer.transform;
            so.FindProperty("activeItemIcon").objectReferenceValue = activeIconImg;
            so.FindProperty("activeItemNameText").objectReferenceValue = nameText;
            so.FindProperty("activeItemDetailsText").objectReferenceValue = detailsText;
            so.FindProperty("activeItemPanel").objectReferenceValue = topPanel;
            so.FindProperty("rotateButton").objectReferenceValue = rotateBtn;
            so.FindProperty("mirrorButton").objectReferenceValue = mirrorBtn;
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

        private static GameObject CreateMaskedContainer(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float spacing, bool controlChildWidth)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(RectMask2D));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            var layout = obj.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = controlChildWidth;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            return obj;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color, Color outlineColor)
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

            AddSurfaceEffects(obj, outlineColor);
            return obj;
        }

        private static void AddSurfaceEffects(GameObject obj, Color outlineColor)
        {
            var outline = obj.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1f, -1f);
            outline.effectColor = outlineColor;

            var shadow = obj.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(0f, -2f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
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
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = obj.GetComponent<Image>();
            img.color = btnColor;
            img.raycastTarget = true;

            AddSurfaceEffects(obj, SurfaceOutlineColor);

            var btn = obj.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;

            CreateText("Label", obj.transform, label, 13, 9, 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, font);
            return btn;
        }
    }
}
