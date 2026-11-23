using System.Collections.Generic;
using NUnit.Framework;
using TMBS.Core.Events;
using TMBS.Core.Execution;
using TMBS.Core.History;
using TMBS.Core.Metadata;
using TMBS.Core.Pending;
using TMBS.Core.Pipeline;
using TMBS.Core.Selection;
using TMBS.Core.Validation;
using TMBS.Runtime.Config;
using TMBS.UI;
using TMBS.Unity.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace TMBS.Editor.Tests
{
    [TestFixture]
    public sealed class TmbsBuildingUITests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            _createdObjects.Add(obj);
            return obj;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void Theme_ResolveCategoryColor_IsDeterministic_AndHonorsOverrides()
        {
            var theme = new TmbsUIThemeSettings();

            Color walls1 = theme.ResolveCategoryColor("Walls");
            Color walls2 = theme.ResolveCategoryColor("  walls ");
            Color crops = theme.ResolveCategoryColor("Crops");
            Color pipes = theme.ResolveCategoryColor("Pipes");

            Assert.AreEqual(walls1, walls2, "Category color resolution must be case/whitespace insensitive and deterministic.");
            Assert.AreNotEqual(walls1, crops, "Different genres/categories should resolve to distinct colors.");
            Assert.AreNotEqual(crops, pipes, "Different categories should resolve to distinct colors.");

            Color customOverride = new Color(0.12f, 0.88f, 0.34f, 1f);
            theme.categoryOverrides.Add(new TmbsCategoryColorEntry
            {
                category = "Walls",
                color = customOverride
            });

            Assert.AreEqual(customOverride, theme.ResolveCategoryColor("walls"), "Explicit override must take precedence.");
        }

        [Test]
        public void PaletteSlot_ThreeZoneSetup_HidesZeroCostBadge_AndPreservesCategoryColorWhenSelected()
        {
            var slotObj = Track(new GameObject("TestSlot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(TmbsPaletteSlot)));
            var slot = slotObj.GetComponent<TmbsPaletteSlot>();

            var iconImg = Track(new GameObject("Icon", typeof(RectTransform), typeof(Image))).GetComponent<Image>();
            iconImg.transform.SetParent(slotObj.transform, false);

            var labelTxt = Track(new GameObject("Label", typeof(RectTransform), typeof(Text))).GetComponent<Text>();
            labelTxt.transform.SetParent(slotObj.transform, false);

            var highlightImg = Track(new GameObject("Highlight", typeof(RectTransform), typeof(Image))).GetComponent<Image>();
            highlightImg.transform.SetParent(slotObj.transform, false);

            var categoryBarImg = Track(new GameObject("CategoryBar", typeof(RectTransform), typeof(Image))).GetComponent<Image>();
            categoryBarImg.transform.SetParent(slotObj.transform, false);

            var costContainer = Track(new GameObject("CostPill", typeof(RectTransform), typeof(Image)));
            costContainer.transform.SetParent(slotObj.transform, false);
            var costTxt = Track(new GameObject("CostText", typeof(RectTransform), typeof(Text))).GetComponent<Text>();
            costTxt.transform.SetParent(costContainer.transform, false);

            slot.SetInternalReferences(
                iconImg,
                labelTxt,
                highlightImg,
                slotObj.GetComponent<Button>(),
                costTxt,
                categoryBarImg,
                costContainer,
                layoutElem: slotObj.GetComponent<LayoutElement>());

            var theme = new TmbsUIThemeSettings
            {
                resourceCostPrefix = "",
                selectionBorderColor = new Color(0.42f, 0.72f, 1f, 1f)
            };

            // 1) 1x1 zero-cost item -> cost pill must be hidden
            var simpleData = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            simpleData.displayName = "Stone Floor";
            simpleData.category = "Floors";
            simpleData.isBlueprint = false;
            simpleData.resourceCost = 0;
            simpleData.workCost = 1;

            slot.Setup(simpleData, null, theme);
            Assert.IsFalse(costContainer.activeSelf, "Zero-cost 1x1 buildable should hide the cost badge container.");
            Color expectedCatColor = theme.ResolveCategoryColor("Floors");
            Assert.AreEqual(expectedCatColor, categoryBarImg.color);

            // Selection must keep category color bar intact while changing selection border
            slot.SetSelected(true);
            Assert.AreEqual(expectedCatColor, categoryBarImg.color, "Selecting a slot must not erase its category color strip.");
            Assert.IsTrue(highlightImg.enabled);
            Assert.AreEqual(theme.selectionBorderColor, highlightImg.color);

            // 2) 2x2 blueprint with cost 50 -> cost pill visible without hardcoded '$'
            var bpData = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            bpData.displayName = "Bunker";
            bpData.category = "Structure";
            bpData.isBlueprint = true;
            bpData.footprintSize = new Vector2Int(2, 2);
            bpData.resourceCost = 50;

            slot.Setup(bpData, null, theme);
            Assert.IsTrue(costContainer.activeSelf, "Blueprint with footprint and cost must show the meta badge.");
            Assert.AreEqual("2×2 · 50", costTxt.text);
        }

        [Test]
        public void BuildingUI_HighlightSlotForSelection_DistinguishesSharedTileBuildables_AndDirectTiles()
        {
            var uiGo = Track(new GameObject("Canvas", typeof(Canvas), typeof(RectTransform), typeof(TmbsBuildingUI)));
            var ui = uiGo.GetComponent<TmbsBuildingUI>();

            var scrollGo = Track(new GameObject("SlotsScroll", typeof(RectTransform), typeof(ScrollRect)));
            scrollGo.transform.SetParent(uiGo.transform, false);
            var contentGo = Track(new GameObject("SlotsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)));
            contentGo.transform.SetParent(scrollGo.transform, false);
            scrollGo.GetComponent<ScrollRect>().content = contentGo.GetComponent<RectTransform>();

            var slotPrefabGo = CreateMinimalSlotPrefab("SlotTemplate");
            var slotPrefab = slotPrefabGo.GetComponent<TmbsPaletteSlot>();

            var sharedTile = Track(ScriptableObject.CreateInstance<Tile>());

            var dataA = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            dataA.displayName = "Wall 1x1";
            dataA.category = "Walls";
            dataA.tile = sharedTile;

            var dataB = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            dataB.displayName = "Wall 2x2";
            dataB.category = "Walls";
            dataB.isBlueprint = true;
            dataB.footprintSize = new Vector2Int(2, 2);
            dataB.tile = sharedTile;

            SetPrivateField(ui, "paletteContainer", contentGo.transform);
            SetPrivateField(ui, "paletteScrollRect", scrollGo.GetComponent<ScrollRect>());
            SetPrivateField(ui, "slotPrefab", slotPrefab);
            SetPrivateField(ui, "buildables", new List<TmbsBuildableData> { dataA, dataB });
            SetPrivateField(ui, "directTiles", new List<TileBase> { sharedTile });

            ui.BuildPalette();

            var slots = contentGo.GetComponentsInChildren<TmbsPaletteSlot>(true);
            Assert.AreEqual(3, slots.Length);

            // Selecting dataB must ONLY select dataB's slot, NOT dataA or the directTile slot
            InvokePrivate(ui, "HighlightSlotForSelection", dataB, sharedTile);
            Assert.IsFalse(slots[0].IsSelected, "dataA should not be selected when dataB is active.");
            Assert.IsTrue(slots[1].IsSelected, "dataB should be selected.");
            Assert.IsFalse(slots[2].IsSelected, "DirectTile slot should not be selected when dataB is active.");

            // Selecting raw sharedTile (data == null) must select the DirectTile slot
            InvokePrivate(ui, "HighlightSlotForSelection", null, sharedTile);
            Assert.IsFalse(slots[0].IsSelected);
            Assert.IsFalse(slots[1].IsSelected);
            Assert.IsTrue(slots[2].IsSelected, "DirectTile slot must match when BuildableData is null.");
        }

        [Test]
        public void BuildingUI_ToggleCatalogExpanded_SwitchesBetweenHorizontalRowAndMultiRowGrid()
        {
            var uiGo = Track(new GameObject("Canvas", typeof(Canvas), typeof(RectTransform), typeof(TmbsBuildingUI)));
            var ui = uiGo.GetComponent<TmbsBuildingUI>();

            var dockGo = Track(new GameObject("BottomDock", typeof(RectTransform)));
            dockGo.transform.SetParent(uiGo.transform, false);
            var dockRt = dockGo.GetComponent<RectTransform>();
            dockRt.anchorMin = new Vector2(0.06f, 0f);
            dockRt.anchorMax = new Vector2(0.94f, 0f);
            dockRt.sizeDelta = new Vector2(0f, 168f);

            var scrollGo = Track(new GameObject("SlotsScroll", typeof(RectTransform), typeof(ScrollRect)));
            scrollGo.transform.SetParent(dockGo.transform, false);
            var contentGo = Track(new GameObject("SlotsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.content = contentGo.GetComponent<RectTransform>();

            var expandBtnGo = Track(new GameObject("BtnExpand", typeof(RectTransform), typeof(Image), typeof(Button)));
            expandBtnGo.transform.SetParent(dockGo.transform, false);
            var expandLbl = Track(new GameObject("Label", typeof(RectTransform), typeof(Text))).GetComponent<Text>();
            expandLbl.transform.SetParent(expandBtnGo.transform, false);

            SetPrivateField(ui, "bottomDockPanel", dockRt);
            SetPrivateField(ui, "paletteScrollRect", scrollRect);
            SetPrivateField(ui, "paletteContainer", contentGo.transform);
            SetPrivateField(ui, "toggleExpandCatalogButton", expandBtnGo.GetComponent<Button>());
            SetPrivateField(ui, "compactDockHeight", 168f);
            SetPrivateField(ui, "expandedDockHeight", 340f);

            ui.SetCatalogExpanded(false);
            Assert.IsFalse(ui.IsCatalogExpanded);
            Assert.IsTrue(scrollRect.horizontal);
            Assert.IsFalse(scrollRect.vertical);
            Assert.IsNotNull(contentGo.GetComponent<HorizontalLayoutGroup>());
            Assert.IsNull(contentGo.GetComponent<GridLayoutGroup>());
            Assert.AreEqual(168f, dockRt.sizeDelta.y, 0.01f);

            ui.ToggleCatalogExpanded();
            Assert.IsTrue(ui.IsCatalogExpanded);
            Assert.IsFalse(scrollRect.horizontal);
            Assert.IsTrue(scrollRect.vertical);
            Assert.IsNotNull(contentGo.GetComponent<GridLayoutGroup>());
            Assert.IsNull(contentGo.GetComponent<HorizontalLayoutGroup>());
            Assert.AreEqual(340f, dockRt.sizeDelta.y, 0.01f);
        }

        [Test]
        public void BuildingUI_CategoryTabs_IncludesGeneralForUncategorized_AndFiltersWithoutHijackingSelection()
        {
            var uiGo = Track(new GameObject("Canvas", typeof(Canvas), typeof(RectTransform), typeof(TmbsBuildingUI)));
            var ui = uiGo.GetComponent<TmbsBuildingUI>();

            var tabsScrollGo = Track(new GameObject("TabsScroll", typeof(RectTransform), typeof(ScrollRect)));
            tabsScrollGo.transform.SetParent(uiGo.transform, false);
            var tabsContainer = Track(new GameObject("TabsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)));
            tabsContainer.transform.SetParent(tabsScrollGo.transform, false);
            tabsScrollGo.GetComponent<ScrollRect>().content = tabsContainer.GetComponent<RectTransform>();

            var slotsScrollGo = Track(new GameObject("SlotsScroll", typeof(RectTransform), typeof(ScrollRect)));
            slotsScrollGo.transform.SetParent(uiGo.transform, false);
            var slotsContainer = Track(new GameObject("SlotsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)));
            slotsContainer.transform.SetParent(slotsScrollGo.transform, false);
            slotsScrollGo.GetComponent<ScrollRect>().content = slotsContainer.GetComponent<RectTransform>();

            var slotPrefabGo = CreateMinimalSlotPrefab("SlotTemplate");

            var uncategorized = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            uncategorized.displayName = "Basic Tile";
            uncategorized.category = "";

            var walls = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            walls.displayName = "Brick Wall";
            walls.category = "Walls";

            SetPrivateField(ui, "categoryTabsContainer", tabsContainer.transform);
            SetPrivateField(ui, "categoryTabsScrollRect", tabsScrollGo.GetComponent<ScrollRect>());
            SetPrivateField(ui, "paletteContainer", slotsContainer.transform);
            SetPrivateField(ui, "paletteScrollRect", slotsScrollGo.GetComponent<ScrollRect>());
            SetPrivateField(ui, "slotPrefab", slotPrefabGo.GetComponent<TmbsPaletteSlot>());
            SetPrivateField(ui, "buildables", new List<TmbsBuildableData> { uncategorized, walls });

            ui.BuildCategoryTabs();
            var tabButtons = tabsContainer.GetComponentsInChildren<Button>(true);
            Assert.AreEqual(3, tabButtons.Length, "Expected All, General, and Walls tabs.");

            // Simulate user having cleared selection, then filtering tabs
            ui.OnClearClicked();
            ui.SetCategoryFilter("Walls");

            var slots = slotsContainer.GetComponentsInChildren<TmbsPaletteSlot>(true);
            Assert.AreEqual(1, slots.Length);
            Assert.AreEqual(walls, slots[0].BuildableData);
            Assert.IsFalse(slots[0].IsSelected, "Filtering categories after Clear must not hijack selection.");
        }

        [Test]
        public void BuildableData_ClassificationAndSearch_MatchesAcrossCategorySubcategoryRoleAndTags_AndIsNullSafe()
        {
            var data = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            data.name = "AssetGenerator01";
            data.displayName = "";
            data.id = "";
            data.category = "";
            data.subcategory = "  Power Grid  ";
            data.description = "Supplies clean electricity to nearby structures.";
            data.placementRole = TmbsPlacementRole.Utility;
            data.isBlueprint = false;
            data.footprintSize = new Vector2Int(3, 2);
            data.tags = new List<string> { "  Structural ", null, "HighVoltage" };

            Assert.AreEqual("AssetGenerator01", data.GetEffectiveDisplayName());
            Assert.AreEqual("AssetGenerator01", data.GetEffectiveId());
            Assert.AreEqual("General", data.GetEffectiveCategory());
            Assert.AreEqual("Power Grid", data.GetEffectiveSubcategory());
            Assert.AreEqual(1, data.GetFootprintArea(), "Non-blueprint buildables must report 1x1 area.");

            data.isBlueprint = true;
            Assert.AreEqual(6, data.GetFootprintArea(), "Blueprint buildables must report W*H area.");

            Assert.IsTrue(data.HasTag("structural"));
            Assert.IsTrue(data.HasTag("  HIGHVOLTAGE "));
            Assert.IsFalse(data.HasTag(null));
            Assert.IsFalse(data.HasTag("   "));
            Assert.IsFalse(data.HasTag("decorative"));

            Assert.IsTrue(data.MatchesSearch(null));
            Assert.IsTrue(data.MatchesSearch("   "));
            Assert.IsTrue(data.MatchesSearch("power grid"));
            Assert.IsTrue(data.MatchesSearch("utility"));
            Assert.IsTrue(data.MatchesSearch("electricity"));
            Assert.IsTrue(data.MatchesSearch("highvoltage"));
            Assert.IsTrue(data.MatchesSearch("AssetGenerator01"));
            Assert.IsFalse(data.MatchesSearch("nonexistent_query_xyz"));

            data.tags = null;
            Assert.IsFalse(data.HasTag("structural"), "HasTag must remain null-safe when tags list is null.");
        }

        [Test]
        public void BuildableData_Compare_SortsCorrectlyAcrossAllSortModes()
        {
            var alpha = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            alpha.displayName = "Alpha Wall";
            alpha.category = "Walls";
            alpha.placementRole = TmbsPlacementRole.Wall;
            alpha.tier = 2;
            alpha.sortOrder = 20;
            alpha.resourceCost = 30;
            alpha.workCost = 5;
            alpha.isBlueprint = false;

            var beta = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            beta.displayName = "Beta Generator";
            beta.category = "Utilities";
            beta.placementRole = TmbsPlacementRole.Utility;
            beta.tier = 1;
            beta.sortOrder = 5;
            beta.resourceCost = 10;
            beta.workCost = 15;
            beta.isBlueprint = true;
            beta.footprintSize = new Vector2Int(2, 2);

            Assert.Less(TmbsBuildableData.Compare(beta, alpha, TmbsBuildableSortMode.CatalogOrder), 0);
            Assert.Greater(TmbsBuildableData.Compare(beta, alpha, TmbsBuildableSortMode.CatalogOrder, descending: true), 0);

            Assert.Less(TmbsBuildableData.Compare(alpha, beta, TmbsBuildableSortMode.Name), 0);
            Assert.Greater(TmbsBuildableData.Compare(beta, alpha, TmbsBuildableSortMode.Category), 0); // Utilities > Walls
            Assert.Less(TmbsBuildableData.Compare(alpha, beta, TmbsBuildableSortMode.Role), 0); // Wall (2) < Utility (4)
            Assert.Less(TmbsBuildableData.Compare(beta, alpha, TmbsBuildableSortMode.Tier), 0); // Tier 1 < Tier 2
            Assert.Less(TmbsBuildableData.Compare(beta, alpha, TmbsBuildableSortMode.ResourceCost), 0); // 10 < 30
            Assert.Less(TmbsBuildableData.Compare(alpha, beta, TmbsBuildableSortMode.WorkCost), 0); // 5 < 15
            Assert.Less(TmbsBuildableData.Compare(alpha, beta, TmbsBuildableSortMode.FootprintArea), 0); // 1 < 4

            // Null-safety across ascending and descending
            Assert.AreEqual(0, TmbsBuildableData.Compare(null, null, TmbsBuildableSortMode.Name));
            Assert.Greater(TmbsBuildableData.Compare(null, alpha, TmbsBuildableSortMode.Name), 0);
            Assert.Less(TmbsBuildableData.Compare(alpha, null, TmbsBuildableSortMode.Name, descending: true), 0);
        }

        [Test]
        public void TileSelectionState_RespectsCanRotateAndCanMirrorFlags()
        {
            var tile = Track(ScriptableObject.CreateInstance<Tile>());
            var lockedData = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            lockedData.tile = tile;
            lockedData.canRotate = false;
            lockedData.canMirror = false;

            var state = new TileSelectionState();
            state.UpdateFromSelection(new BuildSelectionData(tile, lockedData));
            state.Rotate();
            state.Mirror();

            Assert.AreEqual(0, state.Transform.Rotation, "Rotation must be blocked when canRotate is false.");
            Assert.IsFalse(state.Transform.Mirrored, "Mirroring must be blocked when canMirror is false.");

            var unlockedData = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            unlockedData.tile = tile;
            unlockedData.canRotate = true;
            unlockedData.canMirror = true;

            state.UpdateFromSelection(new BuildSelectionData(tile, unlockedData));
            state.Rotate();
            state.Mirror();

            Assert.AreEqual(1, state.Transform.Rotation);
            Assert.IsTrue(state.Transform.Mirrored);
        }

        [Test]
        public void OccupancyValidators_RejectPartialBlueprints_AndReportAllOccupiedAsInvalid()
        {
            var gridGo = Track(new GameObject("Grid", typeof(Grid)));
            var tilemapGo = Track(new GameObject("TargetTilemap", typeof(Tilemap), typeof(TilemapRenderer)));
            tilemapGo.transform.SetParent(gridGo.transform, false);
            var tilemap = tilemapGo.GetComponent<Tilemap>();

            var occupiedTile = Track(ScriptableObject.CreateInstance<Tile>());
            tilemap.SetTile(new Vector3Int(1, 0, 0), occupiedTile);

            var occValidator = new OccupancyValidator
            {
                skipOccupiedCells = true,
                markBlockedArea = true
            };
            occValidator.InjectOccupancySource(new TilemapOccupancy(tilemap));

            var bpData = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            bpData.isBlueprint = true;
            bpData.footprintSize = new Vector2Int(2, 2);
            bpData.tile = occupiedTile;

            var bpCtx = new PipelineContext
            {
                Cell = Vector3Int.zero,
                DragBounds = new BoundsInt(0, 0, 0, 2, 2, 1),
                HasDragBounds = true,
                SelectedTile = occupiedTile,
                BuildableData = bpData
            };

            var bpRes = occValidator.Validate(in bpCtx, ValidationMode.Full);
            Assert.IsFalse(bpRes.IsValid, "Blueprint partially overlapping an occupied cell must be rejected.");
            Assert.AreEqual(ValidationFailure.Occupied, bpRes.Failure);

            var singleData = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            singleData.isBlueprint = false;
            singleData.tile = occupiedTile;

            var singleCtx = new PipelineContext
            {
                Cell = new Vector3Int(1, 0, 0),
                HasDragBounds = false,
                SelectedTile = occupiedTile,
                BuildableData = singleData
            };

            var singleRes = occValidator.Validate(in singleCtx, ValidationMode.Full);
            Assert.IsFalse(singleRes.IsValid, "Zero free cells under skipOccupiedCells must report InvalidWith(Occupied).");
            Assert.AreEqual(ValidationFailure.Occupied, singleRes.Failure);

            // PendingOccupancyValidator partial blueprint rejection & AllowReplaceExisting
            var store = new PendingStore();
            store.Add(new PendingOrder(
                "inst",
                new BoundsInt(1, 0, 0, 1, 1, 1),
                new[] { new PendingCell(new Vector3Int(1, 0, 0), occupiedTile, 1, 0, 0, 0) },
                PendingState.ReadyToBuild));

            var pendingOcc = new PendingOccupancyValidator();
            pendingOcc.InjectStore(store);

            var pBpRes = pendingOcc.Validate(in bpCtx, ValidationMode.Full);
            Assert.IsFalse(pBpRes.IsValid, "PendingOccupancyValidator must reject partial blueprint placement.");
            Assert.AreEqual(ValidationFailure.Occupied, pBpRes.Failure);

            pendingOcc.AllowReplaceExisting = true;
            Assert.IsTrue(pendingOcc.Validate(in bpCtx, ValidationMode.Full).IsValid, "AllowReplaceExisting must bypass pending blocking.");

            CellMaskPool.ReclaimAll();
        }

        [Test]
        public void ImmediateBuildExecutor_PlacesBlueprintFootprintTiles_AndDismantlesWholeBlueprintOnErase()
        {
            var gridGo = Track(new GameObject("Grid", typeof(Grid)));
            var tilemapGo = Track(new GameObject("TargetTilemap", typeof(Tilemap), typeof(TilemapRenderer)));
            tilemapGo.transform.SetParent(gridGo.transform, false);
            var tilemap = tilemapGo.GetComponent<Tilemap>();

            var t0 = Track(ScriptableObject.CreateInstance<Tile>());
            var t1 = Track(ScriptableObject.CreateInstance<Tile>());
            var t2 = Track(ScriptableObject.CreateInstance<Tile>());
            var t3 = Track(ScriptableObject.CreateInstance<Tile>());

            var bpData = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            bpData.isBlueprint = true;
            bpData.footprintSize = new Vector2Int(2, 2);
            bpData.maxHealth = 120;
            bpData.tile = t0;
            bpData.footprintTiles = new List<TileBase> { t0, t1, t2, t3 };

            var metadata = new MetadataStore(16);
            var history = new UndoRedoHistory();
            var executor = new ImmediateBuildExecutor(metadata, history, new SimpleEventBus(), false);

            var placeBounds = new BoundsInt(0, 0, 0, 2, 2, 1);
            var placeCtx = new PipelineContext
            {
                InstanceId = "test",
                Cell = Vector3Int.zero,
                DragBounds = placeBounds,
                HasDragBounds = true,
                SelectedTile = t0,
                BuildableData = bpData,
                Decision = ExecutionDecision.Immediate(t0, CellMask.AllTrue(placeBounds))
            };

            executor.Execute(in placeCtx, tilemap);

            Assert.AreEqual(4, metadata.Count);
            Assert.AreEqual(t0, tilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.AreEqual(t1, tilemap.GetTile(new Vector3Int(1, 0, 0)));
            Assert.AreEqual(t2, tilemap.GetTile(new Vector3Int(0, 1, 0)));
            Assert.AreEqual(t3, tilemap.GetTile(new Vector3Int(1, 1, 0)));
            Assert.IsTrue(metadata.TryGet(new Vector3Int(1, 1, 0), out var rec));
            Assert.AreEqual(120, rec.CurrentHealth);
            Assert.AreEqual(placeBounds, rec.FootprintBounds);

            // Erase ONLY 1 cell of the 2x2 blueprint -> whole blueprint should dismantle atomically
            var eraseCell = new Vector3Int(0, 0, 0);
            var eraseCtx = new PipelineContext
            {
                InstanceId = "test",
                Cell = eraseCell,
                AlternateBehaviour = true,
                Decision = ExecutionDecision.Immediate(null, CellMask.AllTrue(new BoundsInt(eraseCell, Vector3Int.one)))
            };

            executor.Execute(in eraseCtx, tilemap);

            Assert.AreEqual(0, metadata.Count, "Dismantling any cell of a multi-cell blueprint must remove all 4 metadata records.");
            Assert.IsNull(tilemap.GetTile(new Vector3Int(1, 1, 0)));

            Assert.IsTrue(history.TryUndo());
            Assert.AreEqual(4, metadata.Count, "Undo after erasing blueprint must restore all 4 cells.");
            Assert.AreEqual(t3, tilemap.GetTile(new Vector3Int(1, 1, 0)));
        }

        [Test]
        public void MetadataStore_EnforcesRecordCapacity_FIFO()
        {
            var store = new MetadataStore(capacity: 2);
            var tile = Track(ScriptableObject.CreateInstance<Tile>());

            store.Set(new BuildRecord(tile, new Vector3Int(0, 0, 0), BuildState.Completed));
            store.Set(new BuildRecord(tile, new Vector3Int(1, 0, 0), BuildState.Completed));
            store.Set(new BuildRecord(tile, new Vector3Int(2, 0, 0), BuildState.Completed));

            Assert.AreEqual(2, store.Count);
            Assert.AreEqual(2, store.GetAll().Count);
            Assert.IsFalse(store.TryGet(new Vector3Int(0, 0, 0), out _), "Oldest record should be evicted when capacity is exceeded.");
            Assert.IsTrue(store.TryGet(new Vector3Int(1, 0, 0), out _));
            Assert.IsTrue(store.TryGet(new Vector3Int(2, 0, 0), out _));
        }

        [Test]
        public void PendingSystem_FixedCostMode_PartialDeliveryCeil_AndUndoAfterCompletion()
        {
            // 1) Partial delivery ceil check when workCost < resourceCost
            var partialCell = new PendingCell(Vector3Int.zero, null, workRequired: 1, workDone: 0, resourceRequired: 4, resourceDelivered: 1);
            Assert.AreEqual(1, partialCell.WorkAllowed(ResourceRequirement.AllowPartialDelivery));

            // 2) FixedPerCell cost mode vs DefinitionProvider & Undo after completion
            var gridGo = Track(new GameObject("Grid", typeof(Grid)));
            var tilemapGo = Track(new GameObject("TargetTilemap", typeof(Tilemap), typeof(TilemapRenderer)));
            tilemapGo.transform.SetParent(gridGo.transform, false);
            var tilemap = tilemapGo.GetComponent<Tilemap>();

            var tile = Track(ScriptableObject.CreateInstance<Tile>());
            var data = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            data.tile = tile;
            data.workCost = 2;
            data.resourceCost = 5;

            var store = new PendingStore();
            var events = new SimpleEventBus();
            var metadata = new MetadataStore(16);
            var history = new UndoRedoHistory();
            var cfg = new TmbsPendingConfig
            {
                costMode = PendingCostMode.FixedPerCell,
                workCost = 7,
                resourceCost = 3,
                resourcePolicy = ResourceRequirement.AllowPartialDelivery,
                replaceExistingPending = true
            };

            var workApi = new PendingWorkApi(store, events, tilemap, metadata, "inst", true, cfg.resourcePolicy, true, true);
            var executor = new PendingBuildExecutor(store, events, cfg, history, workApi);

            var c0 = new Vector3Int(0, 0, 0);
            var ctx = new PipelineContext
            {
                InstanceId = "inst",
                Cell = c0,
                SelectedTile = tile,
                BuildableData = data,
                Decision = ExecutionDecision.Pending(tile, CellMask.AllTrue(new BoundsInt(c0, Vector3Int.one)))
            };

            executor.Execute(in ctx, tilemap);
            Assert.IsTrue(workApi.TryGetCellStatus(c0, out var status));
            Assert.AreEqual(7, status.WorkRequired, "FixedPerCell mode must use TmbsPendingConfig.workCost.");
            Assert.AreEqual(3, status.ResourceRequired, "FixedPerCell mode must use TmbsPendingConfig.resourceCost.");

            // Complete using DefinitionProvider
            cfg.costMode = PendingCostMode.DefinitionProvider;
            var c1 = new Vector3Int(1, 0, 0);
            var ctx1 = new PipelineContext
            {
                InstanceId = "inst",
                Cell = c1,
                SelectedTile = tile,
                BuildableData = data,
                Decision = ExecutionDecision.Pending(tile, CellMask.AllTrue(new BoundsInt(c1, Vector3Int.one)))
            };

            executor.Execute(in ctx1, tilemap);
            Assert.IsTrue(workApi.TryDeliverResources(c1, 5));
            Assert.IsTrue(workApi.TryAddWork(c1, 2));
            Assert.IsTrue(workApi.TryCompleteCell(c1));
            Assert.AreEqual(tile, tilemap.GetTile(c1));
            Assert.IsTrue(metadata.TryGet(c1, out _));

            // Undo completed pending order -> must revert placed tile & metadata
            Assert.IsTrue(history.TryUndo());
            Assert.IsNull(tilemap.GetTile(c1), "Undo of a completed pending order must revert its tile from the tilemap.");
            Assert.IsFalse(metadata.TryGet(c1, out _), "Undo of a completed pending order must remove its BuildRecord.");

            // Redo -> re-queues pending order on c1
            Assert.IsTrue(history.TryRedo());
            Assert.IsTrue(workApi.TryGetCellStatus(c1, out _), "Redo must re-queue the pending order.");

            // Replace existing order on c0 & verify Undo restores replaced order
            executor.Execute(in ctx, tilemap);
            Assert.IsTrue(workApi.TryGetCellStatus(c0, out _));
            Assert.IsTrue(history.TryUndo());
            Assert.IsTrue(workApi.TryGetCellStatus(c0, out _), "Undo after replacing an existing pending order must restore the prior order.");
        }

        [Test]
        public void BuildingUI_AppliesPixelPerfect1280x720_AndTogglesRotateMirrorButtonInteractability()
        {
            var uiGo = Track(new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(RectTransform), typeof(TmbsBuildingUI)));
            var canvas = uiGo.GetComponent<Canvas>();
            canvas.pixelPerfect = false;
            var scaler = uiGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var ui = uiGo.GetComponent<TmbsBuildingUI>();

            var rotateBtnGo = Track(new GameObject("BtnRotate", typeof(RectTransform), typeof(Image), typeof(Button)));
            rotateBtnGo.transform.SetParent(uiGo.transform, false);
            var mirrorBtnGo = Track(new GameObject("BtnMirror", typeof(RectTransform), typeof(Image), typeof(Button)));
            mirrorBtnGo.transform.SetParent(uiGo.transform, false);

            var rotateBtn = rotateBtnGo.GetComponent<Button>();
            var mirrorBtn = mirrorBtnGo.GetComponent<Button>();

            SetPrivateField(ui, "rotateButton", rotateBtn);
            SetPrivateField(ui, "mirrorButton", mirrorBtn);

            InvokePrivate(ui, "ApplyPixelPerfectAndTextCrispness");

            Assert.IsTrue(canvas.pixelPerfect, "TmbsBuildingUI must enforce Canvas.pixelPerfect.");
            Assert.AreEqual(new Vector2(1280f, 720f), scaler.referenceResolution, "TmbsBuildingUI must upgrade legacy 1080p reference resolution to 1280x720.");

            var locked = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            locked.tile = Track(ScriptableObject.CreateInstance<Tile>());
            locked.canRotate = false;
            locked.canMirror = false;

            InvokePrivate(ui, "RefreshActiveItemDisplay", locked, locked.tile);
            Assert.IsFalse(rotateBtn.interactable, "Rotate button must be disabled when canRotate is false.");
            Assert.IsFalse(mirrorBtn.interactable, "Mirror button must be disabled when canMirror is false.");

            locked.canRotate = true;
            locked.canMirror = true;
            InvokePrivate(ui, "RefreshActiveItemDisplay", locked, locked.tile);
            Assert.IsTrue(rotateBtn.interactable);
            Assert.IsTrue(mirrorBtn.interactable);
        }

        private GameObject CreateMinimalSlotPrefab(string name)
        {
            var go = Track(new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(TmbsPaletteSlot)));
            var slot = go.GetComponent<TmbsPaletteSlot>();

            var icon = Track(new GameObject("Icon", typeof(RectTransform), typeof(Image))).GetComponent<Image>();
            icon.transform.SetParent(go.transform, false);
            var label = Track(new GameObject("Label", typeof(RectTransform), typeof(Text))).GetComponent<Text>();
            label.transform.SetParent(go.transform, false);
            var border = Track(new GameObject("Border", typeof(RectTransform), typeof(Image))).GetComponent<Image>();
            border.transform.SetParent(go.transform, false);
            var catBar = Track(new GameObject("CategoryColorBar", typeof(RectTransform), typeof(Image))).GetComponent<Image>();
            catBar.transform.SetParent(go.transform, false);

            slot.SetInternalReferences(icon, label, border, go.GetComponent<Button>(), categoryBar: catBar, layoutElem: go.GetComponent<LayoutElement>());
            return go;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
            method.Invoke(target, args);
        }
    }
}
