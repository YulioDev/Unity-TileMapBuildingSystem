using System;
using System.Collections.Generic;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using TMBS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TMBS.Editor
{
    public static class TmbsBuildingUIBuilder
    {
        private const string PrefabPath = "Assets/TMBS/UI/TmbsBuildingUI.prefab";
        private const string SlotPrefabPath = "Assets/TMBS/UI/TmbsPaletteSlot.prefab";

        [MenuItem("Tools/TMBS/1. Configure Scene UI", false, 100)]
        public static GameObject CreateOrUpdateSceneUI()
        {
            var slotPrefab = CreateOrUpdateSlotPrefab();

            var buildeable = UnityEngine.Object.FindAnyObjectByType<BuildeableTilemap>();
            if (buildeable == null)
            {
                Debug.LogWarning("TMBS: No 'BuildeableTilemap' found in the active scene.");
            }

            EnsureEventSystem();

            GameObject canvasObj = FindExistingUiCanvas();
            if (canvasObj != null && PrefabUtility.IsPartOfPrefabInstance(canvasObj))
            {
                PrefabUtility.UnpackPrefabInstance(canvasObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            if (canvasObj == null)
            {
                canvasObj = new GameObject("TMBS_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }

            var canvas = canvasObj.GetComponent<Canvas>() ?? canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;

            var scaler = canvasObj.GetComponent<CanvasScaler>() ?? canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 64f;

            if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            var buildingUI = canvasObj.GetComponent<TmbsBuildingUI>() ?? canvasObj.AddComponent<TmbsBuildingUI>();

            for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(canvasObj.transform.GetChild(i).gameObject);
            }

            var defaultTheme = new TmbsUIThemeSettings();
            Font defaultFont = ResolveDefaultFont();

            // 1. Top HUD Panel
            var topPanel = CreatePanel(
                "TopHUD_Panel",
                canvasObj.transform,
                new Vector2(0.015f, 0.985f),
                new Vector2(0.985f, 0.985f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -10f),
                new Vector2(0f, 76f),
                defaultTheme.panelBackgroundColor,
                defaultTheme.panelBorderColor);

            var leftZone = CreateRectChild("LeftInfo_Zone", topPanel.transform, new Vector2(0.008f, 0.08f), new Vector2(0.49f, 0.92f));
            var leftLayout = leftZone.AddComponent<HorizontalLayoutGroup>();
            leftLayout.spacing = 10f;
            leftLayout.childAlignment = TextAnchor.MiddleLeft;
            leftLayout.childControlWidth = true;
            leftLayout.childControlHeight = true;
            leftLayout.childForceExpandWidth = false;
            leftLayout.childForceExpandHeight = true;

            var iconFrame = new GameObject("ActiveIcon_Frame", typeof(RectTransform), typeof(LayoutElement));
            iconFrame.transform.SetParent(leftZone.transform, false);
            var iconFrameLe = iconFrame.GetComponent<LayoutElement>();
            iconFrameLe.minWidth = 60f;
            iconFrameLe.preferredWidth = 60f;
            iconFrameLe.minHeight = 60f;
            iconFrameLe.preferredHeight = 60f;
            iconFrameLe.flexibleWidth = 0f;
            iconFrameLe.flexibleHeight = 0f;

            var iconObj = new GameObject("ActiveIcon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(iconFrame.transform, false);
            var iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.06f, 0.06f);
            iconRt.anchorMax = new Vector2(0.94f, 0.94f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var activeIconImg = iconObj.GetComponent<Image>();
            activeIconImg.preserveAspect = true;
            activeIconImg.raycastTarget = false;

            var infoStack = new GameObject("InfoText_Stack", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            infoStack.transform.SetParent(leftZone.transform, false);
            var infoStackLe = infoStack.GetComponent<LayoutElement>();
            infoStackLe.flexibleWidth = 1f;
            infoStackLe.minWidth = 120f;
            var infoStackLayout = infoStack.GetComponent<VerticalLayoutGroup>();
            infoStackLayout.spacing = 2f;
            infoStackLayout.childAlignment = TextAnchor.UpperLeft;
            infoStackLayout.childControlWidth = true;
            infoStackLayout.childControlHeight = true;
            infoStackLayout.childForceExpandWidth = true;
            infoStackLayout.childForceExpandHeight = true;

            var headerRow = new GameObject("HeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            headerRow.transform.SetParent(infoStack.transform, false);
            var headerRowLe = headerRow.GetComponent<LayoutElement>();
            headerRowLe.minHeight = 18f;
            headerRowLe.preferredHeight = 20f;
            var headerRowLayout = headerRow.GetComponent<HorizontalLayoutGroup>();
            headerRowLayout.spacing = 8f;
            headerRowLayout.childAlignment = TextAnchor.MiddleLeft;
            headerRowLayout.childControlWidth = true;
            headerRowLayout.childControlHeight = true;
            headerRowLayout.childForceExpandWidth = false;
            headerRowLayout.childForceExpandHeight = true;

            var titleText = CreateText(
                "TitleText",
                headerRow.transform,
                "BUILD MODE",
                14,
                10,
                16,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                defaultTheme.headerAccentColor,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                defaultFont);

            var pendingPanel = CreatePanel(
                "PendingOrders_Panel",
                headerRow.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                defaultTheme.pendingPanelColor,
                defaultTheme.pendingAccentColor);
            var pendingLayout = pendingPanel.AddComponent<HorizontalLayoutGroup>();
            pendingLayout.padding = new RectOffset(8, 8, 1, 1);
            pendingLayout.childAlignment = TextAnchor.MiddleCenter;
            pendingLayout.childControlWidth = true;
            pendingLayout.childControlHeight = true;
            pendingLayout.childForceExpandWidth = true;
            pendingLayout.childForceExpandHeight = true;
            var pendingCountText = CreateText(
                "PendingOrders_Text",
                pendingPanel.transform,
                "Pending orders: 0",
                12,
                9,
                13,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color(1f, 0.85f, 0.3f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                defaultFont);
            pendingPanel.SetActive(false);

            var nameText = CreateText(
                "ActiveItemName",
                infoStack.transform,
                "Building: None",
                15,
                10,
                16,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(0.95f, 0.96f, 0.98f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                defaultFont);
            var nameLe = nameText.gameObject.AddComponent<LayoutElement>();
            nameLe.minHeight = 18f;
            nameLe.preferredHeight = 20f;

            var detailsText = CreateText(
                "ActiveItemDetails",
                infoStack.transform,
                "Select a buildable from the palette below",
                12,
                9,
                13,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                new Color(0.75f, 0.78f, 0.83f, 1f),
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                defaultFont);
            var detailsLe = detailsText.gameObject.AddComponent<LayoutElement>();
            detailsLe.minHeight = 15f;
            detailsLe.preferredHeight = 18f;

            var toolbarZone = CreateRectChild("ToolbarActions_Container", topPanel.transform, new Vector2(0.50f, 0.14f), new Vector2(0.99f, 0.86f));
            var toolbarLayout = toolbarZone.AddComponent<HorizontalLayoutGroup>();
            toolbarLayout.spacing = 6f;
            toolbarLayout.childAlignment = TextAnchor.MiddleRight;
            toolbarLayout.childControlWidth = true;
            toolbarLayout.childControlHeight = true;
            toolbarLayout.childForceExpandWidth = true;
            toolbarLayout.childForceExpandHeight = true;

            var rotateBtn = CreateToolbarButton("BtnRotate", toolbarZone.transform, "Rotate [R]", defaultTheme.neutralButtonColor, defaultTheme.panelBorderColor, defaultFont);
            var mirrorBtn = CreateToolbarButton("BtnMirror", toolbarZone.transform, "Mirror [F]", defaultTheme.neutralButtonColor, defaultTheme.panelBorderColor, defaultFont);
            var eraseBtn = CreateToolbarButton("BtnErase", toolbarZone.transform, "Erase [X]", defaultTheme.eraseIdleColor, defaultTheme.panelBorderColor, defaultFont);
            var clearBtn = CreateToolbarButton("BtnClear", toolbarZone.transform, "Clear [Esc]", defaultTheme.clearButtonColor, defaultTheme.panelBorderColor, defaultFont);
            var undoBtn = CreateToolbarButton("BtnUndo", toolbarZone.transform, "Undo [Ctrl+Z]", defaultTheme.neutralButtonColor, defaultTheme.panelBorderColor, defaultFont);
            var redoBtn = CreateToolbarButton("BtnRedo", toolbarZone.transform, "Redo [Ctrl+Y]", defaultTheme.neutralButtonColor, defaultTheme.panelBorderColor, defaultFont);

            // 2. Bottom Palette Dock
            var bottomPanel = CreatePanel(
                "BottomPalette_Panel",
                canvasObj.transform,
                new Vector2(0.06f, 0f),
                new Vector2(0.94f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 12f),
                new Vector2(0f, 168f),
                defaultTheme.panelBackgroundColor,
                defaultTheme.panelBorderColor);

            var dockHeader = CreateRectChild("DockHeader", bottomPanel.transform, new Vector2(0.012f, 1f), new Vector2(0.988f, 1f));
            var dockHeaderRt = dockHeader.GetComponent<RectTransform>();
            dockHeaderRt.pivot = new Vector2(0.5f, 1f);
            dockHeaderRt.anchoredPosition = new Vector2(0f, -6f);
            dockHeaderRt.sizeDelta = new Vector2(0f, 24f);

            var paletteHeaderText = CreateText(
                "PaletteTitle",
                dockHeader.transform,
                "BUILD CATALOG  \u00B7  Hotkeys 1-9",
                13,
                10,
                14,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(0.88f, 0.90f, 0.94f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0.82f, 1f),
                Vector2.zero,
                defaultFont);

            var expandBtn = CreateButton(
                "BtnExpandCatalog",
                dockHeader.transform,
                "Expand \u229E",
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                defaultTheme.neutralButtonColor,
                defaultTheme.panelBorderColor,
                defaultFont);
            var expandRt = expandBtn.GetComponent<RectTransform>();
            expandRt.pivot = new Vector2(1f, 0.5f);
            expandRt.anchoredPosition = Vector2.zero;
            expandRt.sizeDelta = new Vector2(108f, 0f);

            var tabsScroll = CreateHorizontalScrollContainer(
                "CategoryTabs_ScrollView",
                "CategoryTabs_Container",
                bottomPanel.transform,
                new Vector2(0.012f, 1f),
                new Vector2(0.988f, 1f),
                new RectOffset(4, 4, 2, 2),
                6f,
                24f,
                out var tabsScrollRect,
                out var tabsContainer);
            var tabsScrollRt = tabsScroll.GetComponent<RectTransform>();
            tabsScrollRt.pivot = new Vector2(0.5f, 1f);
            tabsScrollRt.anchoredPosition = new Vector2(0f, -33f);
            tabsScrollRt.sizeDelta = new Vector2(0f, 30f);

            var slotsScroll = CreateHorizontalScrollContainer(
                "Slots_ScrollView",
                "SlotsContainer",
                bottomPanel.transform,
                new Vector2(0.012f, 0f),
                new Vector2(0.988f, 1f),
                new RectOffset(6, 6, 4, 4),
                10f,
                28f,
                out var slotsScrollRect,
                out var slotsContainer);
            var slotsScrollRt = slotsScroll.GetComponent<RectTransform>();
            slotsScrollRt.offsetMin = new Vector2(0f, 6f);
            slotsScrollRt.offsetMax = new Vector2(0f, -66f);

            // 3. Validation Toast Panel
            var toastPanel = CreatePanel(
                "ValidationToast_Panel",
                canvasObj.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 190f),
                new Vector2(520f, 44f),
                defaultTheme.infoToastColor,
                defaultTheme.panelBorderColor);

            var toastText = CreateText(
                "ToastMessage",
                toastPanel.transform,
                "Validation notice",
                15,
                10,
                17,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Color.white,
                Vector2.zero,
                Vector2.one,
                new Vector2(12f, 2f),
                defaultFont);
            toastText.rectTransform.offsetMax = new Vector2(-12f, -2f);
            toastPanel.SetActive(false);

            // 4. Collect & Sort Buildables
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

            foundBuildables.Sort((a, b) => TmbsBuildableData.Compare(a, b, TmbsBuildableSortMode.CatalogOrder));

            // 5. Wire Serialized Fields on TmbsBuildingUI
            var so = new SerializedObject(buildingUI);
            SetObjectProperty(so, "buildeableTilemap", buildeable);
            SetObjectProperty(so, "paletteContainer", slotsContainer.transform);
            SetObjectProperty(so, "slotPrefab", slotPrefab);
            SetObjectProperty(so, "categoryTabsContainer", tabsContainer.transform);
            SetObjectProperty(so, "activeItemIcon", activeIconImg);
            SetObjectProperty(so, "activeItemNameText", nameText);
            SetObjectProperty(so, "activeItemDetailsText", detailsText);
            SetObjectProperty(so, "activeItemPanel", topPanel);
            SetObjectProperty(so, "rotateButton", rotateBtn);
            SetObjectProperty(so, "mirrorButton", mirrorBtn);
            SetObjectProperty(so, "undoButton", undoBtn);
            SetObjectProperty(so, "redoButton", redoBtn);
            SetObjectProperty(so, "clearButton", clearBtn);
            SetObjectProperty(so, "eraseButton", eraseBtn);
            SetObjectProperty(so, "toastPanel", toastPanel);
            SetObjectProperty(so, "toastText", toastText);
            SetObjectProperty(so, "pendingPanel", pendingPanel);
            SetObjectProperty(so, "pendingCountText", pendingCountText);
            SetObjectProperty(so, "bottomDockPanel", bottomPanel.GetComponent<RectTransform>());
            SetObjectProperty(so, "paletteScrollRect", slotsScrollRect);
            SetObjectProperty(so, "categoryTabsScrollRect", tabsScrollRect);
            SetObjectProperty(so, "toggleExpandCatalogButton", expandBtn);
            SetObjectProperty(so, "titleText", titleText);
            SetObjectProperty(so, "paletteHeaderText", paletteHeaderText);

            var buildablesProp = so.FindProperty("buildables");
            if (buildablesProp != null)
            {
                buildablesProp.ClearArray();
                for (int i = 0; i < foundBuildables.Count; i++)
                {
                    buildablesProp.InsertArrayElementAtIndex(i);
                    buildablesProp.GetArrayElementAtIndex(i).objectReferenceValue = foundBuildables[i];
                }
            }

            so.ApplyModifiedProperties();

            SetLayerRecursively(canvasObj, ResolveUiLayer());

            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            Debug.Log($"TMBS: Scene UI configured with {foundBuildables.Count} available buildables (TabsScroll: {tabsScroll.name}, SlotsScroll: {slotsScroll.name}).");
            return canvasObj;
        }

        [MenuItem("Tools/TMBS/2. Save UI as Prefab", false, 101)]
        public static void SaveAsPrefab()
        {
            var canvasObj = FindExistingUiCanvas();
            if (canvasObj == null)
            {
                canvasObj = CreateOrUpdateSceneUI();
            }

            EnsureUiFolder();

            var buildingUI = canvasObj.GetComponent<TmbsBuildingUI>();
            var cachedBuildeable = buildingUI != null ? buildingUI.BuildeableTilemap : null;

            // Clear scene-specific tilemap link inside prefab asset so runtime auto-detection works in any scene.
            if (buildingUI != null)
            {
                var so = new SerializedObject(buildingUI);
                SetObjectProperty(so, "buildeableTilemap", null);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasObj, PrefabPath, InteractionMode.UserAction);

            if (buildingUI != null && cachedBuildeable != null)
            {
                var so = new SerializedObject(buildingUI);
                SetObjectProperty(so, "buildeableTilemap", cachedBuildeable);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"TMBS: UI prefab saved at: {PrefabPath}");
        }

        [MenuItem("Tools/TMBS/3. Rebuild Slot Prefab", false, 102)]
        public static TmbsPaletteSlot CreateOrUpdateSlotPrefab()
        {
            EnsureUiFolder();

            var defaultTheme = new TmbsUIThemeSettings();
            Font defaultFont = ResolveDefaultFont();

            var rootObj = new GameObject(
                "TmbsPaletteSlot",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement),
                typeof(TmbsPaletteSlot));

            var rootRt = rootObj.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(84f, 88f);

            var rootImg = rootObj.GetComponent<Image>();
            rootImg.color = defaultTheme.slotNormalColor;
            rootImg.raycastTarget = true;

            var slotBtn = rootObj.GetComponent<Button>();
            slotBtn.targetGraphic = rootImg;
            ApplyNormalizedButtonColors(slotBtn);

            var layoutElem = rootObj.GetComponent<LayoutElement>();
            layoutElem.minWidth = 76f;
            layoutElem.minHeight = 80f;
            layoutElem.preferredWidth = 84f;
            layoutElem.preferredHeight = 88f;
            layoutElem.flexibleWidth = 0f;
            layoutElem.flexibleHeight = 0f;

            var outline = rootObj.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.effectColor = defaultTheme.panelBorderColor;

            var rootShadow = rootObj.AddComponent<Shadow>();
            rootShadow.effectDistance = new Vector2(0f, -2f);
            rootShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);

            // Category Color Bar (bottom strip)
            var categoryBarObj = new GameObject("CategoryColorBar", typeof(RectTransform), typeof(Image));
            categoryBarObj.transform.SetParent(rootObj.transform, false);
            SetStretchAnchors(categoryBarObj.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0.045f));
            var categoryBarImg = categoryBarObj.GetComponent<Image>();
            categoryBarImg.color = defaultTheme.ResolveCategoryColor("General");
            categoryBarImg.raycastTarget = false;

            // Highlight Border (top strip, disabled when unselected)
            var highlightObj = new GameObject("HighlightBorder", typeof(RectTransform), typeof(Image));
            highlightObj.transform.SetParent(rootObj.transform, false);
            SetStretchAnchors(highlightObj.GetComponent<RectTransform>(), new Vector2(0f, 0.955f), new Vector2(1f, 1f));
            var highlightImg = highlightObj.GetComponent<Image>();
            highlightImg.color = defaultTheme.selectionBorderColor;
            highlightImg.raycastTarget = false;
            highlightImg.enabled = false;

            // Item Icon (middle zone)
            var iconObj = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(rootObj.transform, false);
            SetStretchAnchors(iconObj.GetComponent<RectTransform>(), new Vector2(0.15f, 0.28f), new Vector2(0.85f, 0.76f));
            var iconImg = iconObj.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Item Label (bottom zone above category strip)
            var labelTxt = CreateText(
                "ItemLabel",
                rootObj.transform,
                "Block",
                12,
                10,
                12,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                defaultTheme.slotNormalLabelColor,
                new Vector2(0.06f, 0.06f),
                new Vector2(0.94f, 0.26f),
                Vector2.zero,
                defaultFont);
            labelTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelTxt.verticalOverflow = VerticalWrapMode.Truncate;
            var labelShadow = labelTxt.gameObject.AddComponent<Shadow>();
            labelShadow.effectDistance = new Vector2(1f, -1f);
            labelShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);

            // Hotkey Badge Pill (top-left)
            var hotkeyBadgeObj = new GameObject("HotkeyBadge", typeof(RectTransform), typeof(Image));
            hotkeyBadgeObj.transform.SetParent(rootObj.transform, false);
            SetStretchAnchors(hotkeyBadgeObj.GetComponent<RectTransform>(), new Vector2(0.06f, 0.78f), new Vector2(0.28f, 0.95f));
            var hotkeyBg = hotkeyBadgeObj.GetComponent<Image>();
            hotkeyBg.color = defaultTheme.badgePillColor;
            hotkeyBg.raycastTarget = false;

            var hotkeyTxt = CreateText(
                "HotkeyText",
                hotkeyBadgeObj.transform,
                "1",
                10,
                9,
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                defaultTheme.slotNormalLabelColor,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                defaultFont);
            hotkeyBadgeObj.SetActive(false);

            // Cost / Footprint Badge Pill (top-right)
            var costPillObj = new GameObject("CostBadge_Pill", typeof(RectTransform), typeof(Image));
            costPillObj.transform.SetParent(rootObj.transform, false);
            SetStretchAnchors(costPillObj.GetComponent<RectTransform>(), new Vector2(0.34f, 0.78f), new Vector2(0.94f, 0.95f));
            var costPillImg = costPillObj.GetComponent<Image>();
            costPillImg.color = defaultTheme.badgePillColor;
            costPillImg.raycastTarget = false;

            var costTxt = CreateText(
                "CostBadge",
                costPillObj.transform,
                string.Empty,
                10,
                8,
                11,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                defaultTheme.badgeTextColor,
                Vector2.zero,
                Vector2.one,
                new Vector2(3f, 0f),
                defaultFont);
            costTxt.rectTransform.offsetMax = new Vector2(-3f, 0f);
            costTxt.supportRichText = false;
            costPillObj.SetActive(false);

            SetLayerRecursively(rootObj, ResolveUiLayer());

            var slotComponent = rootObj.GetComponent<TmbsPaletteSlot>();
            var so = new SerializedObject(slotComponent);
            SetObjectProperty(so, "iconImage", iconImg);
            SetObjectProperty(so, "labelText", labelTxt);
            SetObjectProperty(so, "highlightBorder", highlightImg);
            SetObjectProperty(so, "slotButton", slotBtn);
            SetObjectProperty(so, "costBadgeText", costTxt);
            SetObjectProperty(so, "categoryColorBar", categoryBarImg);
            SetObjectProperty(so, "costBadgeContainer", costPillObj);
            SetObjectProperty(so, "hotkeyBadge", hotkeyBadgeObj);
            SetObjectProperty(so, "hotkeyText", hotkeyTxt);
            SetObjectProperty(so, "slotLayoutElement", layoutElem);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(rootObj, SlotPrefabPath);
            UnityEngine.Object.DestroyImmediate(rootObj);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<TmbsPaletteSlot>(SlotPrefabPath);
        }

        private static GameObject FindExistingUiCanvas()
        {
            var existingUi = UnityEngine.Object.FindAnyObjectByType<TmbsBuildingUI>();
            if (existingUi != null)
                return existingUi.gameObject;

            return GameObject.Find("TMBS_Canvas") ?? GameObject.Find("TmbsBuildingUI");
        }

        private static void EnsureUiFolder()
        {
            if (AssetDatabase.IsValidFolder("Assets/TMBS/UI"))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/TMBS"))
            {
                AssetDatabase.CreateFolder("Assets", "TMBS");
            }

            AssetDatabase.CreateFolder("Assets/TMBS", "UI");
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null && EventSystem.current == null)
            {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }
        }

        private static Font ResolveDefaultFont()
        {
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
            }
            return defaultFont;
        }

        private static int ResolveUiLayer()
        {
            int layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : 5;
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null)
                return;

            obj.layer = layer;
            Transform t = obj.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
            }
        }

        private static GameObject CreateRectChild(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            SetStretchAnchors(obj.GetComponent<RectTransform>(), anchorMin, anchorMax);
            return obj;
        }

        private static void SetStretchAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreateHorizontalScrollContainer(
            string scrollName,
            string contentName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            RectOffset padding,
            float spacing,
            float scrollSensitivity,
            out ScrollRect scrollRect,
            out GameObject contentObj)
        {
            var scrollObj = new GameObject(scrollName, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollObj.transform.SetParent(parent, false);

            var scrollRt = scrollObj.GetComponent<RectTransform>();
            SetStretchAnchors(scrollRt, anchorMin, anchorMax);

            var raycastBg = scrollObj.GetComponent<Image>();
            raycastBg.color = new Color(0f, 0f, 0f, 0f);
            raycastBg.raycastTarget = true;

            scrollRect = scrollObj.GetComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = scrollSensitivity;
            scrollRect.viewport = scrollRt;

            contentObj = new GameObject(contentName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(scrollObj.transform, false);

            var contentRt = contentObj.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var layout = contentObj.GetComponent<HorizontalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var fitter = contentObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.content = contentRt;
            return scrollObj;
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 pos,
            Vector2 size,
            Color color,
            Color outlineColor)
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

        private static Text CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            int minSize,
            int maxSize,
            FontStyle style,
            TextAnchor align,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Font font)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = Vector2.zero;

            var txt = obj.GetComponent<Text>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.resizeTextForBestFit = false;
            txt.resizeTextMinSize = minSize;
            txt.resizeTextMaxSize = maxSize;
            txt.fontStyle = style;
            txt.alignment = align;
            txt.color = color;
            txt.font = font;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateToolbarButton(
            string name,
            Transform parent,
            string label,
            Color btnColor,
            Color outlineColor,
            Font font)
        {
            var btn = CreateButton(name, parent, label, Vector2.zero, Vector2.one, btnColor, outlineColor, font);
            var layoutElem = btn.gameObject.AddComponent<LayoutElement>();
            layoutElem.minWidth = 82f;
            layoutElem.preferredWidth = 110f;
            return btn;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color btnColor,
            Color outlineColor,
            Font font)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            SetStretchAnchors(obj.GetComponent<RectTransform>(), anchorMin, anchorMax);

            var img = obj.GetComponent<Image>();
            img.color = btnColor;
            img.raycastTarget = true;

            AddSurfaceEffects(obj, outlineColor);

            var btn = obj.GetComponent<Button>();
            btn.targetGraphic = img;
            ApplyNormalizedButtonColors(btn);

            CreateText("Label", obj.transform, label, 12, 10, 13, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, Vector2.zero, font);
            return btn;
        }

        private static void ApplyNormalizedButtonColors(Button btn)
        {
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
        }

        private static void SetObjectProperty(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
        }
    }
}
