using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMBS.Runtime.Config;
using TMBS.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace TMBS.Editor.InputConfigurator
{
    /// <summary>
    /// Visual Studio &amp; gallery view integrated into the TMBS Settings window for creating,
    /// classifying, sorting, and configuring TmbsBuildableData assets across game genres.
    /// </summary>
    public sealed class BuildablesPageView : VisualElement
    {
        private const string AllCategoriesLabel = "All Categories";
        private const string AllSubcategoriesLabel = "All Subcategories";
        private const string AllRolesLabel = "All Roles";
        private const string AllTiersLabel = "All Tiers";
        private const string AllTypesLabel = "All Types";
        private const string NoneSubcategoryLabel = "(None)";

        private const string CustomCategoriesPrefKey = "TMBS.Buildables.CustomCategories";
        private const string CustomSubcategoriesPrefKey = "TMBS.Buildables.CustomSubcategories";

        private static readonly string[] TypeFilterOptions =
        {
            AllTypesLabel,
            "Single Tile (1x1)",
            "Blueprints (>1x1)",
            "Missing Tile",
            "Zero Resource Cost",
            "Rotatable",
            "Blocks Movement"
        };

        private static readonly TmbsUIThemeSettings FallbackTheme = new TmbsUIThemeSettings();

        private readonly List<TmbsBuildableData> _allBuildables = new List<TmbsBuildableData>();
        private readonly List<TmbsBuildableData> _filteredBuildables = new List<TmbsBuildableData>();
        private readonly SortedSet<string> _customCategories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SortedSet<string> _customSubcategories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        private TmbsRootConfig _targetConfig;
        private TmbsBuildableData _selected;
        private string _categoryRenameSource = "General";
        private bool _gridView;
        private bool _sortDescending;
        private TmbsBuildableSortMode _sortMode = TmbsBuildableSortMode.CatalogOrder;

        private string _searchQuery = string.Empty;
        private string _categoryFilter = AllCategoriesLabel;
        private string _subcategoryFilter = AllSubcategoriesLabel;
        private string _roleFilter = AllRolesLabel;
        private string _tierFilter = AllTiersLabel;
        private string _typeFilter = AllTypesLabel;
        private string _tagFilter = string.Empty;

        private bool _isUpdatingDetailFields;
        private string _defaultCreateFolder = "Assets/TMBS/Buildables";

        public int TotalCount => _allBuildables.Count;
        public int FilteredCount => _filteredBuildables.Count;
        public int MissingTileCount { get; private set; }
        public TmbsBuildableData SelectedBuildable => _selected;

        public event Action CatalogChanged;

        // Top & Quick Create
        private Button _toggleQuickCreateBtn;
        private VisualElement _quickCreatePanel;
        private TextField _qcNameField;
        private PopupField<string> _qcCategoryPopup;
        private TextField _qcCategoryField;
        private PopupField<string> _qcSubcategoryPopup;
        private TextField _qcSubcategoryField;
        private EnumField _qcRoleField;
        private ObjectField _qcTileField;
        private ObjectField _qcSpriteField;
        private TextField _qcFolderField;
        private Toggle _qcAutoTileToggle;
        private Label _statusBannerLabel;

        // Filter & Sort Toolbar
        private TextField _searchField;
        private PopupField<string> _categoryPopup;
        private PopupField<string> _subcategoryPopup;
        private PopupField<string> _rolePopup;
        private PopupField<string> _tierPopup;
        private PopupField<string> _typePopup;
        private EnumField _sortModeField;
        private Button _sortDirBtn;
        private Button _viewModeBtn;
        private Label _summaryCountLabel;
        private Label _missingWarningBadge;
        private VisualElement _tagFilterBar;

        // Master Pane
        private VisualElement _masterItemsContainer;

        // Detail Pane
        private VisualElement _detailEmptyState;
        private ScrollView _detailContentScroll;
        private VisualElement _detailCatDot;
        private IMGUIContainer _detailThumb;
        private Label _detailTitleLabel;
        private Label _defaultBuildableBadge;
        private Label _detailAssetPathLabel;
        private Button _setDefaultBuildableBtn;
        private VisualElement _detailMissingTileBanner;

        private TextField _detDisplayNameField;
        private TextField _detIdField;
        private TextField _detDescriptionField;

        private PopupField<string> _detCategoryPopup;
        private TextField _detCategoryField;
        private VisualElement _detNewCategoryRow;
        private TextField _detNewCategoryInput;
        private VisualElement _detCategoryChipsRow;

        private PopupField<string> _detSubcategoryPopup;
        private TextField _detSubcategoryField;
        private VisualElement _detNewSubcategoryRow;
        private TextField _detNewSubcategoryInput;
        private VisualElement _detSubcatPresetRow;

        private EnumField _detRoleField;
        private IntegerField _detTierField;
        private IntegerField _detSortOrderField;
        private VisualElement _detActiveTagsContainer;
        private TextField _detNewTagField;
        private VisualElement _detProjectTagsRow;

        private ObjectField _detTileField;
        private ObjectField _detSpriteField;
        private Button _detCreateTileBtn;
        private Button _detExtractSpriteBtn;

        private IntegerField _detWorkCostField;
        private IntegerField _detResourceCostField;
        private Toggle _detIsBlueprintToggle;
        private VisualElement _detBlueprintGroup;
        private Vector2IntField _detFootprintField;
        private Label _detFootprintAreaLabel;
        private VisualElement _detFootprintGrid;

        private IntegerField _detMaxHealthField;
        private Toggle _detCanRotateToggle;
        private Toggle _detCanMirrorToggle;
        private Toggle _detBlocksMovementToggle;

        public BuildablesPageView()
        {
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 1f;

            LoadCustomTaxonomy();
            BuildHeaderAndActionBar();
            BuildQuickCreatePanel();
            BuildFilterAndSortToolbar();
            BuildMasterDetailWorkspace();
            BindDetailCallbacks();
            Refresh();
        }

        public void Refresh(TmbsRootConfig targetConfig)
        {
            _targetConfig = targetConfig;
            Refresh();
        }

        public void Refresh()
        {
            ReloadAssets();

            if (_selected == null && _filteredBuildables.Count > 0)
                _selected = _filteredBuildables[0];

            RebuildDynamicFilterChoices();
            ApplyFiltersAndSort();
            RebuildTagFilterBar();
            RebuildMasterCards();
            RefreshDetailAll();
            RefreshSummaryLabels();
            CatalogChanged?.Invoke();
        }

        public void SelectBuildable(TmbsBuildableData buildable)
        {
            ReloadAssets();

            if (buildable != null)
            {
                _selected = buildable;
                RebuildDynamicFilterChoices();
                ApplyFiltersAndSort();

                // Ensure the requested asset is visible if active filters hid it
                if (!_filteredBuildables.Contains(buildable))
                {
                    ResetFiltersInternal();
                    ApplyFiltersAndSort();
                }
            }
            else if (_selected == null && _filteredBuildables.Count > 0)
            {
                _selected = _filteredBuildables[0];
                RebuildDynamicFilterChoices();
                ApplyFiltersAndSort();
            }

            RebuildTagFilterBar();
            RebuildMasterCards();
            RefreshDetailAll();
            RefreshSummaryLabels();
            CatalogChanged?.Invoke();
        }

        private void BuildHeaderAndActionBar()
        {
            var title = new Label("4. Buildables Studio & Gallery");
            title.AddToClassList("tmbs-section-header");
            Add(title);

            var desc = new Label(
                "Create buildable assets effortlessly, configure visual/construction/gameplay properties, and organize across multiple genres with flexible classification and sorting.");
            desc.AddToClassList("tmbs-section-desc");
            Add(desc);

            var bar = new VisualElement();
            bar.AddToClassList("tmbs-preset-bar");

            _toggleQuickCreateBtn = new Button(ToggleQuickCreatePanel)
            {
                text = "+ New Buildable",
                tooltip = "Open inline Quick-Create form to create a new TmbsBuildableData asset."
            };
            _toggleQuickCreateBtn.AddToClassList("tmbs-primary-btn");
            _toggleQuickCreateBtn.AddToClassList("tmbs-preset-btn");
            bar.Add(_toggleQuickCreateBtn);

            var fromSelectionBtn = new Button(CreateFromSelectedProjectAssets)
            {
                text = "+ From Selected Sprites / Tiles",
                tooltip = "Select Sprite, Texture2D, or TileBase assets in the Project window and generate buildables automatically."
            };
            fromSelectionBtn.AddToClassList("tmbs-preset-btn");
            bar.Add(fromSelectionBtn);

            var syncUiBtn = new Button(SyncToSceneUI)
            {
                text = "Sync to Scene UI",
                tooltip = "Run TmbsBuildingUIBuilder.CreateOrUpdateSceneUI() so active scene UI uses the latest catalog order."
            };
            syncUiBtn.AddToClassList("tmbs-preset-btn");
            bar.Add(syncUiBtn);

            var refreshBtn = new Button(() =>
            {
                SetStatus("Catalog refreshed.");
                Refresh();
            })
            {
                text = "Refresh"
            };
            refreshBtn.AddToClassList("tmbs-preset-btn");
            bar.Add(refreshBtn);

            _statusBannerLabel = new Label(string.Empty);
            _statusBannerLabel.style.fontSize = 11;
            _statusBannerLabel.style.marginLeft = 8;
            _statusBannerLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _statusBannerLabel.style.flexShrink = 1;
            bar.Add(_statusBannerLabel);

            Add(bar);
        }

        private void BuildQuickCreatePanel()
        {
            _quickCreatePanel = new VisualElement();
            _quickCreatePanel.AddToClassList("tmbs-card-panel");
            _quickCreatePanel.style.display = DisplayStyle.None;

            var hdr = new Label("Quick Create Buildable Asset");
            hdr.AddToClassList("tmbs-b-section-title");
            _quickCreatePanel.Add(hdr);

            var row1 = new VisualElement();
            row1.AddToClassList("tmbs-b-toolbar-row");

            _qcNameField = new TextField("Display Name") { value = "New Buildable" };
            _qcNameField.style.flexGrow = 1;
            _qcNameField.style.minWidth = 180;
            _qcNameField.style.marginRight = 6;
            row1.Add(_qcNameField);

            _qcRoleField = new EnumField("Role", TmbsPlacementRole.Structure);
            _qcRoleField.style.width = 190;
            row1.Add(_qcRoleField);

            _quickCreatePanel.Add(row1);

            var taxRow = new VisualElement();
            taxRow.AddToClassList("tmbs-b-toolbar-row");

            _qcCategoryPopup = new PopupField<string>("Category", new List<string> { "General" }, 0);
            _qcCategoryPopup.style.width = 195;
            _qcCategoryPopup.style.marginRight = 4;
            _qcCategoryPopup.RegisterValueChangedCallback(evt =>
            {
                if (!string.IsNullOrEmpty(evt.newValue))
                    _qcCategoryField.SetValueWithoutNotify(evt.newValue);
            });
            taxRow.Add(_qcCategoryPopup);

            _qcCategoryField = new TextField { value = "General", tooltip = "Choose an existing category or type a new one." };
            _qcCategoryField.style.flexGrow = 1;
            _qcCategoryField.style.minWidth = 115;
            _qcCategoryField.style.marginRight = 8;
            taxRow.Add(_qcCategoryField);

            _qcSubcategoryPopup = new PopupField<string>("Subcategory", new List<string> { NoneSubcategoryLabel }, 0);
            _qcSubcategoryPopup.style.width = 200;
            _qcSubcategoryPopup.style.marginRight = 4;
            _qcSubcategoryPopup.RegisterValueChangedCallback(evt =>
            {
                string val = string.Equals(evt.newValue, NoneSubcategoryLabel, StringComparison.Ordinal)
                    ? string.Empty
                    : (evt.newValue ?? string.Empty);
                _qcSubcategoryField.SetValueWithoutNotify(val);
            });
            taxRow.Add(_qcSubcategoryPopup);

            _qcSubcategoryField = new TextField { value = string.Empty, tooltip = "Optional subcategory or type a new one." };
            _qcSubcategoryField.style.flexGrow = 1;
            _qcSubcategoryField.style.minWidth = 115;
            taxRow.Add(_qcSubcategoryField);

            _quickCreatePanel.Add(taxRow);

            var row2 = new VisualElement();
            row2.AddToClassList("tmbs-b-toolbar-row");

            _qcTileField = new ObjectField("Tile Asset")
            {
                objectType = typeof(TileBase),
                allowSceneObjects = false
            };
            _qcTileField.style.flexGrow = 1;
            _qcTileField.style.minWidth = 180;
            _qcTileField.style.marginRight = 6;
            row2.Add(_qcTileField);

            _qcSpriteField = new ObjectField("Preview Sprite")
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false
            };
            _qcSpriteField.style.flexGrow = 1;
            _qcSpriteField.style.minWidth = 180;
            _qcSpriteField.style.marginRight = 6;
            row2.Add(_qcSpriteField);

            _qcAutoTileToggle = new Toggle("Auto-create Tile from Sprite")
            {
                value = true,
                tooltip = "If Tile Asset is empty and Preview Sprite is set, creates a matching Tile asset automatically."
            };
            row2.Add(_qcAutoTileToggle);

            _quickCreatePanel.Add(row2);

            var row3 = new VisualElement();
            row3.AddToClassList("tmbs-b-toolbar-row");

            _qcFolderField = new TextField("Folder") { value = _defaultCreateFolder };
            _qcFolderField.style.flexGrow = 1;
            _qcFolderField.style.marginRight = 6;
            row3.Add(_qcFolderField);

            var browseBtn = new Button(BrowseQuickCreateFolder) { text = "Browse..." };
            browseBtn.AddToClassList("tmbs-preset-btn");
            row3.Add(browseBtn);

            var createBtn = new Button(ExecuteQuickCreate) { text = "Create & Edit" };
            createBtn.AddToClassList("tmbs-primary-btn");
            createBtn.AddToClassList("tmbs-preset-btn");
            row3.Add(createBtn);

            var cancelBtn = new Button(() => SetQuickCreateVisible(false)) { text = "Cancel" };
            cancelBtn.AddToClassList("tmbs-preset-btn");
            row3.Add(cancelBtn);

            _quickCreatePanel.Add(row3);
            Add(_quickCreatePanel);
        }

        private void BuildFilterAndSortToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("tmbs-b-toolbar");

            // Row 1: Search & Classification Filters
            var row1 = new VisualElement();
            row1.AddToClassList("tmbs-b-toolbar-row");

            _searchField = new TextField { tooltip = "Search by name, id, category, subcategory, role, description, or tag." };
            _searchField.AddToClassList("tmbs-b-search");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue ?? string.Empty;
                OnFilterOrSortChanged();
            });
            row1.Add(_searchField);

            var clearSearchBtn = new Button(() =>
            {
                _searchQuery = string.Empty;
                _searchField.SetValueWithoutNotify(string.Empty);
                OnFilterOrSortChanged();
            })
            {
                text = "Clear",
                tooltip = "Clear search query"
            };
            clearSearchBtn.AddToClassList("tmbs-preset-btn");
            clearSearchBtn.style.marginRight = 6;
            row1.Add(clearSearchBtn);

            _categoryPopup = new PopupField<string>(new List<string> { AllCategoriesLabel }, 0);
            _categoryPopup.AddToClassList("tmbs-b-filter-popup");
            _categoryPopup.RegisterValueChangedCallback(evt =>
            {
                _categoryFilter = evt.newValue ?? AllCategoriesLabel;
                OnFilterOrSortChanged();
            });
            row1.Add(_categoryPopup);

            _subcategoryPopup = new PopupField<string>(new List<string> { AllSubcategoriesLabel }, 0);
            _subcategoryPopup.AddToClassList("tmbs-b-filter-popup");
            _subcategoryPopup.RegisterValueChangedCallback(evt =>
            {
                _subcategoryFilter = evt.newValue ?? AllSubcategoriesLabel;
                OnFilterOrSortChanged();
            });
            row1.Add(_subcategoryPopup);

            var roleChoices = new List<string> { AllRolesLabel };
            roleChoices.AddRange(Enum.GetNames(typeof(TmbsPlacementRole)));
            _rolePopup = new PopupField<string>(roleChoices, 0);
            _rolePopup.AddToClassList("tmbs-b-filter-popup");
            _rolePopup.RegisterValueChangedCallback(evt =>
            {
                _roleFilter = evt.newValue ?? AllRolesLabel;
                OnFilterOrSortChanged();
            });
            row1.Add(_rolePopup);

            _tierPopup = new PopupField<string>(new List<string> { AllTiersLabel }, 0);
            _tierPopup.AddToClassList("tmbs-b-filter-popup");
            _tierPopup.RegisterValueChangedCallback(evt =>
            {
                _tierFilter = evt.newValue ?? AllTiersLabel;
                OnFilterOrSortChanged();
            });
            row1.Add(_tierPopup);

            _typePopup = new PopupField<string>(new List<string>(TypeFilterOptions), 0);
            _typePopup.AddToClassList("tmbs-b-filter-popup");
            _typePopup.RegisterValueChangedCallback(evt =>
            {
                _typeFilter = evt.newValue ?? AllTypesLabel;
                OnFilterOrSortChanged();
            });
            row1.Add(_typePopup);

            toolbar.Add(row1);

            // Row 2: Sorting, View toggle & Summary
            var row2 = new VisualElement();
            row2.AddToClassList("tmbs-b-toolbar-row");

            var sortLbl = new Label("Sort:");
            sortLbl.style.fontSize = 11;
            sortLbl.style.marginRight = 4;
            row2.Add(sortLbl);

            _sortModeField = new EnumField(_sortMode);
            _sortModeField.style.minWidth = 132;
            _sortModeField.style.marginRight = 4;
            _sortModeField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is TmbsBuildableSortMode mode)
                {
                    _sortMode = mode;
                    OnFilterOrSortChanged();
                }
            });
            row2.Add(_sortModeField);

            _sortDirBtn = new Button(() =>
            {
                _sortDescending = !_sortDescending;
                UpdateSortDirButtonText();
                OnFilterOrSortChanged();
            });
            _sortDirBtn.AddToClassList("tmbs-preset-btn");
            UpdateSortDirButtonText();
            row2.Add(_sortDirBtn);

            _viewModeBtn = new Button(() =>
            {
                _gridView = !_gridView;
                UpdateViewModeButtonText();
                RebuildMasterCards();
            });
            _viewModeBtn.AddToClassList("tmbs-preset-btn");
            UpdateViewModeButtonText();
            row2.Add(_viewModeBtn);

            var resetFiltersBtn = new Button(() =>
            {
                ResetFiltersInternal();
                OnFilterOrSortChanged();
            })
            {
                text = "Reset Filters"
            };
            resetFiltersBtn.AddToClassList("tmbs-preset-btn");
            row2.Add(resetFiltersBtn);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            row2.Add(spacer);

            _missingWarningBadge = new Label();
            _missingWarningBadge.AddToClassList("tmbs-pill");
            _missingWarningBadge.AddToClassList("tmbs-pill--warn");
            _missingWarningBadge.style.marginRight = 6;
            row2.Add(_missingWarningBadge);

            _summaryCountLabel = new Label();
            _summaryCountLabel.AddToClassList("tmbs-pill");
            _summaryCountLabel.AddToClassList("tmbs-pill--muted");
            row2.Add(_summaryCountLabel);

            toolbar.Add(row2);

            // Row 3: Dynamic Tag Filter Bar
            _tagFilterBar = new VisualElement();
            _tagFilterBar.AddToClassList("tmbs-b-tag-bar");
            toolbar.Add(_tagFilterBar);

            Add(toolbar);
        }

        private void BuildMasterDetailWorkspace()
        {
            var split = new VisualElement();
            split.AddToClassList("tmbs-b-split");

            // Left: Master Gallery
            var masterPane = new VisualElement();
            masterPane.AddToClassList("tmbs-b-master");

            var masterScroll = new ScrollView();
            masterScroll.style.flexGrow = 1;
            _masterItemsContainer = new VisualElement();
            _masterItemsContainer.AddToClassList("tmbs-b-master-list");
            masterScroll.Add(_masterItemsContainer);
            masterPane.Add(masterScroll);
            split.Add(masterPane);

            // Right: Detail Studio
            var detailPane = new VisualElement();
            detailPane.AddToClassList("tmbs-b-detail");

            _detailEmptyState = new VisualElement();
            _detailEmptyState.style.paddingTop = 28;
            _detailEmptyState.style.alignItems = Align.Center;
            var emptyLbl = new Label("Select a buildable card on the left or click '+ New Buildable' above.");
            emptyLbl.AddToClassList("tmbs-subtitle");
            _detailEmptyState.Add(emptyLbl);
            detailPane.Add(_detailEmptyState);

            _detailContentScroll = new ScrollView();
            _detailContentScroll.style.flexGrow = 1;
            BuildDetailInspectorSections(_detailContentScroll);
            detailPane.Add(_detailContentScroll);

            split.Add(detailPane);
            Add(split);
        }

        private void BuildDetailInspectorSections(VisualElement container)
        {
            // Top Asset Header
            var headerSection = new VisualElement();
            headerSection.AddToClassList("tmbs-b-section");
            headerSection.style.flexDirection = FlexDirection.Row;
            headerSection.style.alignItems = Align.Center;

            _detailThumb = new IMGUIContainer();
            _detailThumb.AddToClassList("tmbs-b-thumb");
            _detailThumb.style.width = 68;
            _detailThumb.style.height = 68;
            _detailThumb.style.minWidth = 68;
            _detailThumb.style.minHeight = 68;
            _detailThumb.onGUIHandler = () => DrawThumbnail(_selected, _detailThumb);
            headerSection.Add(_detailThumb);

            var titleCol = new VisualElement();
            titleCol.style.flexGrow = 1;
            titleCol.style.marginRight = 6;

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            _detailCatDot = new VisualElement();
            _detailCatDot.style.width = 10;
            _detailCatDot.style.height = 10;
            _detailCatDot.style.borderTopLeftRadius = 5;
            _detailCatDot.style.borderTopRightRadius = 5;
            _detailCatDot.style.borderBottomLeftRadius = 5;
            _detailCatDot.style.borderBottomRightRadius = 5;
            _detailCatDot.style.marginRight = 6;
            nameRow.Add(_detailCatDot);

            _detailTitleLabel = new Label("—");
            _detailTitleLabel.style.fontSize = 14;
            _detailTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameRow.Add(_detailTitleLabel);

            _defaultBuildableBadge = new Label("★ Default");
            _defaultBuildableBadge.AddToClassList("tmbs-pill");
            _defaultBuildableBadge.AddToClassList("tmbs-pill--ok");
            _defaultBuildableBadge.style.marginLeft = 8;
            _defaultBuildableBadge.style.display = DisplayStyle.None;
            nameRow.Add(_defaultBuildableBadge);

            titleCol.Add(nameRow);

            _detailAssetPathLabel = new Label("—");
            _detailAssetPathLabel.AddToClassList("tmbs-subtitle");
            _detailAssetPathLabel.style.marginTop = 2;
            titleCol.Add(_detailAssetPathLabel);

            headerSection.Add(titleCol);

            var assetActionsCol = new VisualElement();
            assetActionsCol.style.flexDirection = FlexDirection.Row;
            assetActionsCol.style.flexWrap = Wrap.Wrap;

            _setDefaultBuildableBtn = new Button(ToggleSelectedAsDefaultBuildable)
            {
                text = "Set Default",
                tooltip = "Assign or unassign this buildable as TmbsRootConfig.defaultBuildable."
            };
            _setDefaultBuildableBtn.AddToClassList("tmbs-preset-btn");
            assetActionsCol.Add(_setDefaultBuildableBtn);

            var pingBtn = new Button(PingSelectedAsset) { text = "Ping" };
            pingBtn.AddToClassList("tmbs-preset-btn");
            assetActionsCol.Add(pingBtn);

            var dupBtn = new Button(DuplicateSelectedAsset) { text = "Duplicate" };
            dupBtn.AddToClassList("tmbs-preset-btn");
            assetActionsCol.Add(dupBtn);

            var delBtn = new Button(DeleteSelectedAsset) { text = "Delete" };
            delBtn.AddToClassList("tmbs-preset-btn");
            assetActionsCol.Add(delBtn);

            headerSection.Add(assetActionsCol);
            container.Add(headerSection);

            // Missing tile warning banner
            _detailMissingTileBanner = new VisualElement();
            _detailMissingTileBanner.AddToClassList("tmbs-pill");
            _detailMissingTileBanner.AddToClassList("tmbs-pill--warn");
            _detailMissingTileBanner.style.marginBottom = 8;
            _detailMissingTileBanner.style.paddingTop = 5;
            _detailMissingTileBanner.style.paddingBottom = 5;
            var warnLbl = new Label(
                "No TileBase assigned: this buildable cannot be placed on a Tilemap until a Tile is assigned (or use 'Create Tile from Sprite' below).");
            warnLbl.style.whiteSpace = WhiteSpace.Normal;
            _detailMissingTileBanner.Add(warnLbl);
            container.Add(_detailMissingTileBanner);

            // Section A: Identity
            var idSection = CreateDetailSection("Identity");
            _detDisplayNameField = new TextField("Display Name");
            idSection.Add(_detDisplayNameField);

            var idRow = new VisualElement();
            idRow.AddToClassList("tmbs-b-field-row");
            _detIdField = new TextField("Unique ID");
            _detIdField.style.flexGrow = 1;
            _detIdField.style.marginRight = 4;
            idRow.Add(_detIdField);

            var autoIdBtn = new Button(AutoGenerateIdForSelected)
            {
                text = "Auto from Name",
                tooltip = "Generate a clean snake_case ID from the display name."
            };
            autoIdBtn.AddToClassList("tmbs-preset-btn");
            idRow.Add(autoIdBtn);
            idSection.Add(idRow);

            _detDescriptionField = new TextField("Description") { multiline = true };
            _detDescriptionField.style.minHeight = 42;
            idSection.Add(_detDescriptionField);
            container.Add(idSection);

            // Section B: Classification & Developer Taxonomy
            var classSection = CreateDetailSection("Classification & Project Taxonomy");

            // Category controls
            var catInputRow = new VisualElement();
            catInputRow.AddToClassList("tmbs-b-field-row");

            _detCategoryPopup = new PopupField<string>("Category", new List<string> { "General" }, 0);
            _detCategoryPopup.style.flexGrow = 1;
            _detCategoryPopup.style.minWidth = 160;
            _detCategoryPopup.style.marginRight = 4;
            catInputRow.Add(_detCategoryPopup);

            _detCategoryField = new TextField { tooltip = "Edit the category directly or use '+ New Category' to add to the project taxonomy." };
            _detCategoryField.style.flexGrow = 1;
            _detCategoryField.style.minWidth = 110;
            _detCategoryField.style.marginRight = 4;
            catInputRow.Add(_detCategoryField);

            var addCatToggleBtn = new Button(ToggleInlineAddCategory)
            {
                text = "+ New Category",
                tooltip = "Create a reusable category for your project and assign it to this block."
            };
            addCatToggleBtn.AddToClassList("tmbs-preset-btn");
            addCatToggleBtn.style.marginRight = 4;
            catInputRow.Add(addCatToggleBtn);

            var renameCatBtn = new Button(RenameSelectedCategoryAcrossProject)
            {
                text = "Rename All",
                tooltip = "Rename this block's previous category across all buildables that shared it."
            };
            renameCatBtn.AddToClassList("tmbs-preset-btn");
            catInputRow.Add(renameCatBtn);

            classSection.Add(catInputRow);

            _detNewCategoryRow = new VisualElement();
            _detNewCategoryRow.AddToClassList("tmbs-b-field-row");
            _detNewCategoryRow.style.display = DisplayStyle.None;
            _detNewCategoryRow.style.marginTop = 2;

            _detNewCategoryInput = new TextField("New Category") { tooltip = "Enter new category name and press Enter or click Add & Assign." };
            _detNewCategoryInput.style.flexGrow = 1;
            _detNewCategoryInput.style.marginRight = 4;
            _detNewCategoryInput.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    AddAndAssignCategoryFromInput();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    _detNewCategoryRow.style.display = DisplayStyle.None;
                    evt.StopPropagation();
                }
            });
            _detNewCategoryRow.Add(_detNewCategoryInput);

            var confirmNewCatBtn = new Button(AddAndAssignCategoryFromInput) { text = "Add & Assign" };
            confirmNewCatBtn.AddToClassList("tmbs-primary-btn");
            confirmNewCatBtn.AddToClassList("tmbs-preset-btn");
            confirmNewCatBtn.style.marginRight = 4;
            _detNewCategoryRow.Add(confirmNewCatBtn);

            var cancelNewCatBtn = new Button(() => _detNewCategoryRow.style.display = DisplayStyle.None) { text = "Cancel" };
            cancelNewCatBtn.AddToClassList("tmbs-preset-btn");
            _detNewCategoryRow.Add(cancelNewCatBtn);
            classSection.Add(_detNewCategoryRow);

            _detCategoryChipsRow = new VisualElement();
            _detCategoryChipsRow.AddToClassList("tmbs-b-tag-bar");
            _detCategoryChipsRow.style.marginBottom = 6;
            classSection.Add(_detCategoryChipsRow);

            // Subcategory controls
            var subInputRow = new VisualElement();
            subInputRow.AddToClassList("tmbs-b-field-row");

            _detSubcategoryPopup = new PopupField<string>("Subcategory", new List<string> { NoneSubcategoryLabel }, 0);
            _detSubcategoryPopup.style.flexGrow = 1;
            _detSubcategoryPopup.style.minWidth = 160;
            _detSubcategoryPopup.style.marginRight = 4;
            subInputRow.Add(_detSubcategoryPopup);

            _detSubcategoryField = new TextField { tooltip = "Optional secondary grouping for this block." };
            _detSubcategoryField.style.flexGrow = 1;
            _detSubcategoryField.style.minWidth = 110;
            _detSubcategoryField.style.marginRight = 4;
            subInputRow.Add(_detSubcategoryField);

            var addSubToggleBtn = new Button(ToggleInlineAddSubcategory)
            {
                text = "+ New Subcategory",
                tooltip = "Create a reusable subcategory and assign it to this block."
            };
            addSubToggleBtn.AddToClassList("tmbs-preset-btn");
            addSubToggleBtn.style.marginRight = 4;
            subInputRow.Add(addSubToggleBtn);

            var clearSubBtn = new Button(ClearSelectedSubcategory)
            {
                text = "Clear",
                tooltip = "Clear subcategory from this block."
            };
            clearSubBtn.AddToClassList("tmbs-preset-btn");
            subInputRow.Add(clearSubBtn);

            classSection.Add(subInputRow);

            _detNewSubcategoryRow = new VisualElement();
            _detNewSubcategoryRow.AddToClassList("tmbs-b-field-row");
            _detNewSubcategoryRow.style.display = DisplayStyle.None;
            _detNewSubcategoryRow.style.marginTop = 2;

            _detNewSubcategoryInput = new TextField("New Subcategory") { tooltip = "Enter new subcategory name and press Enter or click Add & Assign." };
            _detNewSubcategoryInput.style.flexGrow = 1;
            _detNewSubcategoryInput.style.marginRight = 4;
            _detNewSubcategoryInput.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    AddAndAssignSubcategoryFromInput();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    _detNewSubcategoryRow.style.display = DisplayStyle.None;
                    evt.StopPropagation();
                }
            });
            _detNewSubcategoryRow.Add(_detNewSubcategoryInput);

            var confirmNewSubBtn = new Button(AddAndAssignSubcategoryFromInput) { text = "Add & Assign" };
            confirmNewSubBtn.AddToClassList("tmbs-primary-btn");
            confirmNewSubBtn.AddToClassList("tmbs-preset-btn");
            confirmNewSubBtn.style.marginRight = 4;
            _detNewSubcategoryRow.Add(confirmNewSubBtn);

            var cancelNewSubBtn = new Button(() => _detNewSubcategoryRow.style.display = DisplayStyle.None) { text = "Cancel" };
            cancelNewSubBtn.AddToClassList("tmbs-preset-btn");
            _detNewSubcategoryRow.Add(cancelNewSubBtn);
            classSection.Add(_detNewSubcategoryRow);

            _detSubcatPresetRow = new VisualElement();
            _detSubcatPresetRow.AddToClassList("tmbs-b-tag-bar");
            _detSubcatPresetRow.style.marginBottom = 6;
            classSection.Add(_detSubcatPresetRow);

            _detRoleField = new EnumField("Placement Role", TmbsPlacementRole.Structure);
            classSection.Add(_detRoleField);

            var tierSortRow = new VisualElement();
            tierSortRow.AddToClassList("tmbs-b-field-row");
            _detTierField = new IntegerField("Tier") { style = { flexGrow = 1, marginRight = 8 } };
            _detSortOrderField = new IntegerField("Sort Order") { style = { flexGrow = 1 } };
            tierSortRow.Add(_detTierField);
            tierSortRow.Add(_detSortOrderField);
            classSection.Add(tierSortRow);

            var tagsHeader = new Label("Tags (click × to remove)");
            tagsHeader.style.fontSize = 11;
            tagsHeader.style.marginTop = 4;
            tagsHeader.style.marginBottom = 3;
            classSection.Add(tagsHeader);

            _detActiveTagsContainer = new VisualElement();
            _detActiveTagsContainer.AddToClassList("tmbs-b-tag-bar");
            classSection.Add(_detActiveTagsContainer);

            var addTagRow = new VisualElement();
            addTagRow.AddToClassList("tmbs-b-field-row");
            addTagRow.style.marginTop = 4;
            _detNewTagField = new TextField { tooltip = "Enter a tag name and press Add or Enter." };
            _detNewTagField.style.flexGrow = 1;
            _detNewTagField.style.marginRight = 4;
            _detNewTagField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    AddTagFromInput();
                    evt.StopPropagation();
                }
            });
            addTagRow.Add(_detNewTagField);

            var addTagBtn = new Button(AddTagFromInput) { text = "+ Add Tag" };
            addTagBtn.AddToClassList("tmbs-preset-btn");
            addTagRow.Add(addTagBtn);
            classSection.Add(addTagRow);

            _detProjectTagsRow = new VisualElement();
            _detProjectTagsRow.AddToClassList("tmbs-b-tag-bar");
            classSection.Add(_detProjectTagsRow);

            container.Add(classSection);

            // Section C: Visual Assets
            var visSection = CreateDetailSection("Visual & Tile Assets");

            _detTileField = new ObjectField("Tile Asset")
            {
                objectType = typeof(TileBase),
                allowSceneObjects = false
            };
            visSection.Add(_detTileField);

            _detSpriteField = new ObjectField("Preview Sprite")
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false
            };
            visSection.Add(_detSpriteField);

            var visBtnRow = new VisualElement();
            visBtnRow.AddToClassList("tmbs-b-field-row");
            visBtnRow.style.marginTop = 4;

            _detCreateTileBtn = new Button(CreateTileFromSelectedSprite)
            {
                text = "Create Tile from Sprite",
                tooltip = "Creates a Unity Tile asset from the Preview Sprite and assigns it to Tile Asset."
            };
            _detCreateTileBtn.AddToClassList("tmbs-primary-btn");
            _detCreateTileBtn.AddToClassList("tmbs-preset-btn");
            visBtnRow.Add(_detCreateTileBtn);

            _detExtractSpriteBtn = new Button(ExtractSpriteFromSelectedTile)
            {
                text = "Extract Sprite from Tile",
                tooltip = "Copies the sprite resolved from Tile Asset into Preview Sprite."
            };
            _detExtractSpriteBtn.AddToClassList("tmbs-preset-btn");
            visBtnRow.Add(_detExtractSpriteBtn);

            visSection.Add(visBtnRow);
            container.Add(visSection);

            // Section D: Construction & Blueprint Properties
            var constSection = CreateDetailSection("Construction Costs & Footprint");

            var costRow = new VisualElement();
            costRow.AddToClassList("tmbs-b-field-row");
            _detWorkCostField = new IntegerField("Work Cost / Cell") { style = { flexGrow = 1, marginRight = 8 } };
            _detResourceCostField = new IntegerField("Resource Cost / Cell") { style = { flexGrow = 1 } };
            costRow.Add(_detWorkCostField);
            costRow.Add(_detResourceCostField);
            constSection.Add(costRow);

            _detIsBlueprintToggle = new Toggle("Is Blueprint (Multi-Tile Structure)");
            constSection.Add(_detIsBlueprintToggle);

            _detBlueprintGroup = new VisualElement();
            _detBlueprintGroup.style.marginTop = 4;

            _detFootprintField = new Vector2IntField("Footprint Size (W × H)");
            _detBlueprintGroup.Add(_detFootprintField);

            var fpPresetsRow = new VisualElement();
            fpPresetsRow.AddToClassList("tmbs-b-field-row");
            Vector2Int[] fpPresets =
            {
                new Vector2Int(1, 1),
                new Vector2Int(2, 1),
                new Vector2Int(2, 2),
                new Vector2Int(3, 2),
                new Vector2Int(3, 3),
                new Vector2Int(4, 4)
            };
            for (int i = 0; i < fpPresets.Length; i++)
            {
                Vector2Int size = fpPresets[i];
                var btn = new Button(() => ApplyFootprintPreset(size)) { text = $"{size.x}x{size.y}" };
                btn.AddToClassList("tmbs-b-preset-chip");
                fpPresetsRow.Add(btn);
            }
            _detBlueprintGroup.Add(fpPresetsRow);

            _detFootprintAreaLabel = new Label("Footprint: 1x1 (1 cell)");
            _detFootprintAreaLabel.AddToClassList("tmbs-subtitle");
            _detBlueprintGroup.Add(_detFootprintAreaLabel);

            _detFootprintGrid = new VisualElement();
            _detFootprintGrid.AddToClassList("tmbs-b-footprint-grid");
            _detBlueprintGroup.Add(_detFootprintGrid);

            constSection.Add(_detBlueprintGroup);
            container.Add(constSection);

            // Section E: Gameplay & Placement
            var gpSection = CreateDetailSection("Gameplay & Placement");

            _detMaxHealthField = new IntegerField("Max Health / Durability")
            {
                tooltip = "0 = indestructible or durability not used."
            };
            gpSection.Add(_detMaxHealthField);

            var toggleRow = new VisualElement();
            toggleRow.AddToClassList("tmbs-b-field-row");
            toggleRow.style.marginTop = 4;

            _detCanRotateToggle = new Toggle("Can Rotate") { style = { marginRight = 14 } };
            _detCanMirrorToggle = new Toggle("Can Mirror") { style = { marginRight = 14 } };
            _detBlocksMovementToggle = new Toggle("Blocks Movement");

            toggleRow.Add(_detCanRotateToggle);
            toggleRow.Add(_detCanMirrorToggle);
            toggleRow.Add(_detBlocksMovementToggle);
            gpSection.Add(toggleRow);

            container.Add(gpSection);
        }

        private static VisualElement CreateDetailSection(string headerTitle)
        {
            var section = new VisualElement();
            section.AddToClassList("tmbs-b-section");
            var title = new Label(headerTitle);
            title.AddToClassList("tmbs-b-section-title");
            section.Add(title);
            return section;
        }

        private void BindDetailCallbacks()
        {
            _detDisplayNameField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Change Buildable Name", b => b.displayName = evt.newValue,
                    updateHeader: true, refreshListAndSort: true);
            });

            _detIdField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Change Buildable ID", b => b.id = evt.newValue,
                    refreshListAndSort: true);
            });

            _detDescriptionField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Change Buildable Description", b => b.description = evt.newValue);
            });

            _detCategoryPopup.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyCategoryPreset(evt.newValue);
            });

            _detCategoryField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (_selected != null)
                    _categoryRenameSource = _selected.GetEffectiveCategory();
            });

            _detCategoryField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Change Buildable Category", b => b.category = evt.newValue,
                    updateHeader: true, refreshListAndSort: true, refreshFilters: true);
            });

            _detSubcategoryPopup.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                string value = string.Equals(evt.newValue, NoneSubcategoryLabel, StringComparison.Ordinal)
                    ? string.Empty
                    : evt.newValue;
                ApplySubcategoryPreset(value);
            });

            _detSubcategoryField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Change Buildable Subcategory", b => b.subcategory = evt.newValue,
                    refreshListAndSort: true, refreshFilters: true);
            });

            _detRoleField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                if (evt.newValue is TmbsPlacementRole role)
                {
                    ApplyDetailMutation("Change Placement Role", b => b.placementRole = role,
                        refreshListAndSort: true);
                }
            });

            _detTierField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                int clamped = Mathf.Max(0, evt.newValue);
                if (clamped != evt.newValue)
                    _detTierField.SetValueWithoutNotify(clamped);
                ApplyDetailMutation("Change Buildable Tier", b => b.tier = clamped,
                    refreshListAndSort: true, refreshFilters: true);
            });

            _detSortOrderField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Change Catalog Sort Order", b => b.sortOrder = evt.newValue,
                    refreshListAndSort: true);
            });

            _detTileField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Assign Buildable Tile", b => b.tile = evt.newValue as TileBase,
                    updateHeader: true, refreshListAndSort: true, refreshFilters: true);
            });

            _detSpriteField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Assign Preview Sprite", b => b.previewSprite = evt.newValue as Sprite,
                    updateHeader: true, refreshListAndSort: true);
            });

            _detWorkCostField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                int clamped = Mathf.Max(1, evt.newValue);
                if (clamped != evt.newValue)
                    _detWorkCostField.SetValueWithoutNotify(clamped);
                ApplyDetailMutation("Change Work Cost", b => b.workCost = clamped,
                    refreshListAndSort: true);
            });

            _detResourceCostField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                int clamped = Mathf.Max(0, evt.newValue);
                if (clamped != evt.newValue)
                    _detResourceCostField.SetValueWithoutNotify(clamped);
                ApplyDetailMutation("Change Resource Cost", b => b.resourceCost = clamped,
                    refreshListAndSort: true, refreshFilters: true);
            });

            _detIsBlueprintToggle.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Toggle Blueprint", b => b.isBlueprint = evt.newValue,
                    updateFootprint: true, refreshListAndSort: true, refreshFilters: true);
            });

            _detFootprintField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                var clamped = new Vector2Int(Mathf.Max(1, evt.newValue.x), Mathf.Max(1, evt.newValue.y));
                if (clamped != evt.newValue)
                    _detFootprintField.SetValueWithoutNotify(clamped);
                ApplyDetailMutation("Change Footprint Size", b => b.footprintSize = clamped,
                    updateFootprint: true, refreshListAndSort: true);
            });

            _detMaxHealthField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                int clamped = Mathf.Max(0, evt.newValue);
                if (clamped != evt.newValue)
                    _detMaxHealthField.SetValueWithoutNotify(clamped);
                ApplyDetailMutation("Change Max Health", b => b.maxHealth = clamped);
            });

            _detCanRotateToggle.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Toggle Can Rotate", b => b.canRotate = evt.newValue, refreshFilters: true);
            });

            _detCanMirrorToggle.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Toggle Can Mirror", b => b.canMirror = evt.newValue);
            });

            _detBlocksMovementToggle.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdatingDetailFields) return;
                ApplyDetailMutation("Toggle Blocks Movement", b => b.blocksMovement = evt.newValue, refreshFilters: true);
            });
        }

        private void ApplyDetailMutation(
            string undoLabel,
            Action<TmbsBuildableData> mutator,
            bool syncDetailFields = false,
            bool updateHeader = false,
            bool updateFootprint = false,
            bool refreshTags = false,
            bool refreshListAndSort = false,
            bool refreshFilters = false)
        {
            if (_selected == null || mutator == null)
                return;

            Undo.RecordObject(_selected, undoLabel);
            mutator(_selected);
            EditorUtility.SetDirty(_selected);

            if (syncDetailFields)
                PopulateDetailFieldsWithoutNotify();

            if (updateHeader)
                RefreshDetailHeaderAndVisualButtons();

            if (updateFootprint)
                RefreshFootprintSection();

            if (refreshTags)
                RefreshActiveTagsList();

            if (refreshFilters)
            {
                RebuildDynamicFilterChoices();
                RebuildTagFilterBar();
            }

            if (refreshListAndSort || refreshFilters || refreshTags)
            {
                ApplyFiltersAndSort();
                RebuildMasterCards();
            }

            RefreshSummaryLabels();
            CatalogChanged?.Invoke();
        }

        private void ReloadAssets()
        {
            _allBuildables.Clear();
            string[] guids = AssetDatabase.FindAssets("t:TmbsBuildableData");
            var seen = new HashSet<TmbsBuildableData>();
            string discoveredDir = null;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<TmbsBuildableData>(path);
                if (asset != null && seen.Add(asset))
                {
                    _allBuildables.Add(asset);
                    if (discoveredDir == null && !string.IsNullOrEmpty(path))
                    {
                        string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
                        if (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                            discoveredDir = dir;
                    }
                }
            }

            if (!string.IsNullOrEmpty(discoveredDir))
            {
                _defaultCreateFolder = discoveredDir;
                if (_qcFolderField != null && string.IsNullOrWhiteSpace(_qcFolderField.value))
                    _qcFolderField.SetValueWithoutNotify(_defaultCreateFolder);
            }

            if (_selected != null && !seen.Contains(_selected))
                _selected = null;

            int missing = 0;
            for (int i = 0; i < _allBuildables.Count; i++)
            {
                if (_allBuildables[i].tile == null)
                    missing++;
            }
            MissingTileCount = missing;
        }

        private void RebuildDynamicFilterChoices()
        {
            var categories = GetAllDefinedCategories();
            var subcategories = GetAllDefinedSubcategories();
            var tiers = new SortedSet<int>();

            for (int i = 0; i < _allBuildables.Count; i++)
            {
                tiers.Add(Mathf.Max(0, _allBuildables[i].tier));
            }

            UpdatePopupChoices(_categoryPopup, AllCategoriesLabel, categories, ref _categoryFilter);
            UpdatePopupChoices(_subcategoryPopup, AllSubcategoriesLabel, subcategories, ref _subcategoryFilter);

            var tierOptions = new List<string> { AllTiersLabel };
            foreach (int t in tiers)
                tierOptions.Add($"Tier {t}");
            if (!tierOptions.Contains(_tierFilter))
                _tierFilter = AllTiersLabel;
            _tierPopup.choices = tierOptions;
            _tierPopup.SetValueWithoutNotify(_tierFilter);

            RefreshQuickCreateTaxonomyPopups(categories, subcategories);
            RefreshDetailTaxonomyUI(categories, subcategories);
        }

        private SortedSet<string> GetAllDefinedCategories()
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "General" };
            for (int i = 0; i < _allBuildables.Count; i++)
                set.Add(_allBuildables[i].GetEffectiveCategory());
            foreach (string custom in _customCategories)
            {
                if (!string.IsNullOrWhiteSpace(custom))
                    set.Add(custom.Trim());
            }
            return set;
        }

        private List<string> GetAllDefinedSubcategories(string prioritizeCategory = null)
        {
            var prioritySet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var otherSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _allBuildables.Count; i++)
            {
                var b = _allBuildables[i];
                string sub = b.GetEffectiveSubcategory();
                if (string.IsNullOrEmpty(sub))
                    continue;

                if (!string.IsNullOrEmpty(prioritizeCategory) &&
                    string.Equals(b.GetEffectiveCategory(), prioritizeCategory, StringComparison.OrdinalIgnoreCase))
                {
                    prioritySet.Add(sub);
                }
                else
                {
                    otherSet.Add(sub);
                }
            }

            foreach (string custom in _customSubcategories)
            {
                if (!string.IsNullOrWhiteSpace(custom) && !prioritySet.Contains(custom.Trim()))
                    otherSet.Add(custom.Trim());
            }

            var result = new List<string>(prioritySet.Count + otherSet.Count);
            result.AddRange(prioritySet);
            foreach (string other in otherSet)
            {
                if (!prioritySet.Contains(other))
                    result.Add(other);
            }
            return result;
        }

        private SortedSet<string> GetAllDefinedTags()
        {
            var tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _allBuildables.Count; i++)
            {
                var list = _allBuildables[i].tags;
                if (list == null) continue;
                for (int t = 0; t < list.Count; t++)
                {
                    if (!string.IsNullOrWhiteSpace(list[t]))
                        tags.Add(list[t].Trim());
                }
            }
            return tags;
        }

        private static void UpdatePopupChoices(
            PopupField<string> popup,
            string allLabel,
            IEnumerable<string> items,
            ref string currentSelection)
        {
            var choices = new List<string> { allLabel };
            choices.AddRange(items);

            if (!choices.Contains(currentSelection))
                currentSelection = allLabel;

            popup.choices = choices;
            popup.SetValueWithoutNotify(currentSelection);
        }

        private void RefreshQuickCreateTaxonomyPopups(IEnumerable<string> categories, IEnumerable<string> subcategories)
        {
            if (_qcCategoryPopup != null)
            {
                var catChoices = new List<string>(categories);
                if (catChoices.Count == 0)
                    catChoices.Add("General");

                string currentCat = !string.IsNullOrWhiteSpace(_qcCategoryField?.value)
                    ? _qcCategoryField.value.Trim()
                    : catChoices[0];
                _qcCategoryPopup.choices = catChoices;
                _qcCategoryPopup.SetValueWithoutNotify(catChoices.Contains(currentCat) ? currentCat : catChoices[0]);
            }

            if (_qcSubcategoryPopup != null)
            {
                var subChoices = new List<string> { NoneSubcategoryLabel };
                subChoices.AddRange(subcategories);
                string currentSub = !string.IsNullOrWhiteSpace(_qcSubcategoryField?.value)
                    ? _qcSubcategoryField.value.Trim()
                    : NoneSubcategoryLabel;
                _qcSubcategoryPopup.choices = subChoices;
                _qcSubcategoryPopup.SetValueWithoutNotify(subChoices.Contains(currentSub) ? currentSub : NoneSubcategoryLabel);
            }
        }

        private void RefreshDetailTaxonomyUI(IEnumerable<string> categories = null, IEnumerable<string> allSubcategories = null)
        {
            var catSet = categories != null ? new List<string>(categories) : new List<string>(GetAllDefinedCategories());
            var prioritizedSubs = allSubcategories != null && _selected == null
                ? new List<string>(allSubcategories)
                : GetAllDefinedSubcategories(_selected != null ? _selected.GetEffectiveCategory() : null);

            if (_detCategoryPopup != null)
            {
                _detCategoryPopup.choices = catSet;
                string activeCat = _selected != null ? _selected.GetEffectiveCategory() : catSet[0];
                _detCategoryPopup.SetValueWithoutNotify(catSet.Contains(activeCat) ? activeCat : catSet[0]);
            }

            if (_detSubcategoryPopup != null)
            {
                var subChoices = new List<string> { NoneSubcategoryLabel };
                subChoices.AddRange(prioritizedSubs);
                _detSubcategoryPopup.choices = subChoices;
                string activeSub = _selected != null ? _selected.GetEffectiveSubcategory() : string.Empty;
                _detSubcategoryPopup.SetValueWithoutNotify(!string.IsNullOrEmpty(activeSub) && subChoices.Contains(activeSub)
                    ? activeSub
                    : NoneSubcategoryLabel);
            }

            RefreshDetailCategoryChips(catSet);
            RefreshDetailSubcategoryPresets(prioritizedSubs);
            RefreshProjectTagSuggestions();
        }

        private void RefreshDetailCategoryChips(IEnumerable<string> categories)
        {
            if (_detCategoryChipsRow == null)
                return;

            _detCategoryChipsRow.Clear();

            var usedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _allBuildables.Count; i++)
            {
                string c = _allBuildables[i].GetEffectiveCategory();
                usedCounts[c] = usedCounts.TryGetValue(c, out int n) ? n + 1 : 1;
            }

            string currentCat = _selected != null ? _selected.GetEffectiveCategory() : string.Empty;

            foreach (string cat in categories)
            {
                string captured = cat;
                bool active = string.Equals(currentCat, captured, StringComparison.OrdinalIgnoreCase);

                var chipWrapper = new VisualElement();
                chipWrapper.style.flexDirection = FlexDirection.Row;
                chipWrapper.style.alignItems = Align.Center;
                chipWrapper.style.marginRight = 4;
                chipWrapper.style.marginBottom = 3;

                var chip = new Button(() => ApplyCategoryPreset(captured))
                {
                    text = captured,
                    tooltip = $"Assign category '{captured}' to this block."
                };
                chip.AddToClassList("tmbs-b-tag-chip");
                chip.style.marginRight = 0;
                chip.style.marginBottom = 0;
                chip.style.flexDirection = FlexDirection.Row;
                chip.style.alignItems = Align.Center;

                var dot = new VisualElement();
                dot.style.width = 7;
                dot.style.height = 7;
                dot.style.borderTopLeftRadius = 4;
                dot.style.borderTopRightRadius = 4;
                dot.style.borderBottomLeftRadius = 4;
                dot.style.borderBottomRightRadius = 4;
                dot.style.marginRight = 4;
                dot.style.backgroundColor = ResolveCategoryColor(captured);
                chip.Insert(0, dot);

                if (active)
                    chip.AddToClassList("tmbs-b-tag-chip--active");

                chipWrapper.Add(chip);

                bool isRemovableCustom = !string.Equals(captured, "General", StringComparison.OrdinalIgnoreCase)
                    && _customCategories.Contains(captured)
                    && (!usedCounts.TryGetValue(captured, out int count) || count == 0);

                if (isRemovableCustom)
                {
                    var removeBtn = new Button(() => RemoveUnusedCustomCategory(captured))
                    {
                        text = "×",
                        tooltip = $"Remove unused custom category '{captured}'."
                    };
                    removeBtn.AddToClassList("tmbs-b-tag-chip");
                    removeBtn.style.marginRight = 0;
                    removeBtn.style.marginBottom = 0;
                    removeBtn.style.paddingLeft = 4;
                    removeBtn.style.paddingRight = 4;
                    chipWrapper.Add(removeBtn);
                }

                _detCategoryChipsRow.Add(chipWrapper);
            }
        }

        private void RefreshDetailSubcategoryPresets(IEnumerable<string> existingSubcategories)
        {
            if (_detSubcatPresetRow == null)
                return;

            _detSubcatPresetRow.Clear();
            var subs = new List<string>(existingSubcategories);
            if (subs.Count == 0)
            {
                var emptyLbl = new Label("No subcategories defined yet. Use '+ New Subcategory' to create one.");
                emptyLbl.AddToClassList("tmbs-subtitle");
                _detSubcatPresetRow.Add(emptyLbl);
                return;
            }

            var usedSubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _allBuildables.Count; i++)
            {
                string s = _allBuildables[i].GetEffectiveSubcategory();
                if (!string.IsNullOrEmpty(s))
                    usedSubs.Add(s);
            }

            string currentSub = _selected != null ? _selected.GetEffectiveSubcategory() : string.Empty;

            for (int i = 0; i < subs.Count; i++)
            {
                string sub = subs[i];
                bool active = !string.IsNullOrEmpty(currentSub) &&
                              string.Equals(currentSub, sub, StringComparison.OrdinalIgnoreCase);

                var chipWrapper = new VisualElement();
                chipWrapper.style.flexDirection = FlexDirection.Row;
                chipWrapper.style.alignItems = Align.Center;
                chipWrapper.style.marginRight = 4;
                chipWrapper.style.marginBottom = 3;

                var chip = new Button(() => ApplySubcategoryPreset(sub))
                {
                    text = sub,
                    tooltip = $"Assign subcategory '{sub}' to this block."
                };
                chip.AddToClassList("tmbs-b-tag-chip");
                chip.style.marginRight = 0;
                chip.style.marginBottom = 0;
                if (active)
                    chip.AddToClassList("tmbs-b-tag-chip--active");
                chipWrapper.Add(chip);

                if (_customSubcategories.Contains(sub) && !usedSubs.Contains(sub))
                {
                    var removeBtn = new Button(() => RemoveUnusedCustomSubcategory(sub))
                    {
                        text = "×",
                        tooltip = $"Remove unused custom subcategory '{sub}'."
                    };
                    removeBtn.AddToClassList("tmbs-b-tag-chip");
                    removeBtn.style.marginRight = 0;
                    removeBtn.style.marginBottom = 0;
                    removeBtn.style.paddingLeft = 4;
                    removeBtn.style.paddingRight = 4;
                    chipWrapper.Add(removeBtn);
                }

                _detSubcatPresetRow.Add(chipWrapper);
            }
        }

        private void RefreshProjectTagSuggestions()
        {
            if (_detProjectTagsRow == null)
                return;

            _detProjectTagsRow.Clear();
            var allTags = GetAllDefinedTags();
            if (allTags.Count == 0)
            {
                _detProjectTagsRow.style.display = DisplayStyle.None;
                return;
            }

            _detProjectTagsRow.style.display = DisplayStyle.Flex;
            var suggestLbl = new Label("Project Tags:");
            suggestLbl.AddToClassList("tmbs-subtitle");
            suggestLbl.style.marginRight = 4;
            _detProjectTagsRow.Add(suggestLbl);

            foreach (string tag in allTags)
            {
                string captured = tag;
                bool hasTag = _selected != null && _selected.HasTag(captured);
                var chip = new Button(() => ToggleTagOnSelected(captured))
                {
                    text = hasTag ? $"✓ #{captured}" : $"+ #{captured}",
                    tooltip = hasTag ? $"Click to remove tag '{captured}'." : $"Click to add tag '{captured}'."
                };
                chip.AddToClassList("tmbs-b-tag-chip");
                if (hasTag)
                    chip.AddToClassList("tmbs-b-tag-chip--active");
                _detProjectTagsRow.Add(chip);
            }
        }

        private void ApplyFiltersAndSort()
        {
            _filteredBuildables.Clear();

            for (int i = 0; i < _allBuildables.Count; i++)
            {
                var b = _allBuildables[i];
                if (b == null)
                    continue;

                if (!b.MatchesSearch(_searchQuery))
                    continue;

                if (!string.Equals(_categoryFilter, AllCategoriesLabel, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(b.GetEffectiveCategory(), _categoryFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(_subcategoryFilter, AllSubcategoriesLabel, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(b.GetEffectiveSubcategory(), _subcategoryFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(_roleFilter, AllRolesLabel, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(b.placementRole.ToString(), _roleFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(_tierFilter, AllTiersLabel, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals($"Tier {Mathf.Max(0, b.tier)}", _tierFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!MatchesTypeFilter(b, _typeFilter))
                    continue;

                if (!string.IsNullOrEmpty(_tagFilter) && !b.HasTag(_tagFilter))
                    continue;

                _filteredBuildables.Add(b);
            }

            _filteredBuildables.Sort((a, b) => TmbsBuildableData.Compare(a, b, _sortMode, _sortDescending));
        }

        private static bool MatchesTypeFilter(TmbsBuildableData b, string typeFilter)
        {
            switch (typeFilter)
            {
                case "Single Tile (1x1)":
                    return !b.isBlueprint || b.GetFootprintArea() <= 1;
                case "Blueprints (>1x1)":
                    return b.isBlueprint;
                case "Missing Tile":
                    return b.tile == null;
                case "Zero Resource Cost":
                    return b.resourceCost <= 0;
                case "Rotatable":
                    return b.canRotate;
                case "Blocks Movement":
                    return b.blocksMovement;
                case AllTypesLabel:
                default:
                    return true;
            }
        }

        private void OnFilterOrSortChanged()
        {
            ApplyFiltersAndSort();
            RebuildTagFilterBar();
            RebuildMasterCards();
            RefreshSummaryLabels();
            CatalogChanged?.Invoke();
        }

        private void ResetFiltersInternal()
        {
            _searchQuery = string.Empty;
            _categoryFilter = AllCategoriesLabel;
            _subcategoryFilter = AllSubcategoriesLabel;
            _roleFilter = AllRolesLabel;
            _tierFilter = AllTiersLabel;
            _typeFilter = AllTypesLabel;
            _tagFilter = string.Empty;

            _searchField?.SetValueWithoutNotify(string.Empty);
            _categoryPopup?.SetValueWithoutNotify(AllCategoriesLabel);
            _subcategoryPopup?.SetValueWithoutNotify(AllSubcategoriesLabel);
            _rolePopup?.SetValueWithoutNotify(AllRolesLabel);
            _tierPopup?.SetValueWithoutNotify(AllTiersLabel);
            _typePopup?.SetValueWithoutNotify(AllTypesLabel);
        }

        private void RebuildTagFilterBar()
        {
            _tagFilterBar.Clear();

            var tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _allBuildables.Count; i++)
            {
                var list = _allBuildables[i].tags;
                if (list == null) continue;
                for (int t = 0; t < list.Count; t++)
                {
                    if (!string.IsNullOrWhiteSpace(list[t]))
                        tags.Add(list[t].Trim());
                }
            }

            if (tags.Count == 0)
            {
                _tagFilterBar.style.display = DisplayStyle.None;
                return;
            }

            _tagFilterBar.style.display = DisplayStyle.Flex;

            var lbl = new Label("Tags:");
            lbl.AddToClassList("tmbs-subtitle");
            lbl.style.marginRight = 6;
            _tagFilterBar.Add(lbl);

            var allChip = new Button(() =>
            {
                _tagFilter = string.Empty;
                OnFilterOrSortChanged();
            })
            {
                text = "All Tags"
            };
            allChip.AddToClassList("tmbs-b-tag-chip");
            if (string.IsNullOrEmpty(_tagFilter))
                allChip.AddToClassList("tmbs-b-tag-chip--active");
            _tagFilterBar.Add(allChip);

            foreach (string tag in tags)
            {
                string captured = tag;
                bool active = string.Equals(_tagFilter, captured, StringComparison.OrdinalIgnoreCase);
                var chip = new Button(() =>
                {
                    _tagFilter = active ? string.Empty : captured;
                    OnFilterOrSortChanged();
                })
                {
                    text = "#" + captured
                };
                chip.AddToClassList("tmbs-b-tag-chip");
                if (active)
                    chip.AddToClassList("tmbs-b-tag-chip--active");
                _tagFilterBar.Add(chip);
            }
        }

        private void RebuildMasterCards()
        {
            _masterItemsContainer.Clear();
            _masterItemsContainer.EnableInClassList("tmbs-b-master-grid", _gridView);
            _masterItemsContainer.EnableInClassList("tmbs-b-master-list", !_gridView);

            if (_filteredBuildables.Count == 0)
            {
                var empty = new Label(_allBuildables.Count == 0
                    ? "No TmbsBuildableData assets found in project.\nClick '+ New Buildable' above to create your first one."
                    : "No buildables match the current filters.");
                empty.AddToClassList("tmbs-subtitle");
                empty.style.paddingTop = 20;
                empty.style.paddingLeft = 10;
                empty.style.paddingRight = 10;
                empty.style.whiteSpace = WhiteSpace.Normal;
                _masterItemsContainer.Add(empty);
                return;
            }

            for (int i = 0; i < _filteredBuildables.Count; i++)
            {
                var card = CreateMasterCard(_filteredBuildables[i]);
                _masterItemsContainer.Add(card);
            }
        }

        private VisualElement CreateMasterCard(TmbsBuildableData buildable)
        {
            bool isSelected = buildable == _selected;
            var card = new VisualElement();
            card.AddToClassList("tmbs-b-card");
            if (_gridView)
                card.AddToClassList("tmbs-b-card--grid");
            if (isSelected)
                card.AddToClassList("tmbs-b-card--selected");

            card.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    _selected = buildable;
                    RebuildMasterCards();
                    RefreshDetailAll();
                }
            });

            // Category color strip
            var strip = new VisualElement();
            strip.AddToClassList("tmbs-b-cat-strip");
            strip.style.backgroundColor = ResolveCategoryColor(buildable.GetEffectiveCategory());
            card.Add(strip);

            // Preview thumbnail
            var thumb = new IMGUIContainer();
            thumb.AddToClassList("tmbs-b-thumb");
            thumb.onGUIHandler = () => DrawThumbnail(buildable, thumb);
            card.Add(thumb);

            // Info Column
            var info = new VisualElement();
            info.style.flexGrow = 1;
            info.style.flexShrink = 1;
            info.style.overflow = Overflow.Hidden;

            var nameLbl = new Label(buildable.GetEffectiveDisplayName());
            nameLbl.style.fontSize = 12;
            nameLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLbl.style.textOverflow = TextOverflow.Ellipsis;
            info.Add(nameLbl);

            string sub = buildable.GetEffectiveSubcategory();
            string taxonomy = string.IsNullOrEmpty(sub)
                ? $"{buildable.GetEffectiveCategory()} · {buildable.placementRole}"
                : $"{buildable.GetEffectiveCategory()} / {sub} · {buildable.placementRole}";

            var taxLbl = new Label(taxonomy);
            taxLbl.AddToClassList("tmbs-subtitle");
            taxLbl.style.fontSize = 10;
            taxLbl.style.textOverflow = TextOverflow.Ellipsis;
            info.Add(taxLbl);

            var badges = new VisualElement();
            badges.style.flexDirection = FlexDirection.Row;
            badges.style.flexWrap = Wrap.Wrap;
            badges.style.marginTop = 3;

            var size = buildable.GetSize();
            if (buildable.isBlueprint || size.x > 1 || size.y > 1)
                badges.Add(CreateMiniBadge($"{size.x}x{size.y}", "tmbs-pill--muted"));

            badges.Add(CreateMiniBadge($"C:{buildable.resourceCost}", "tmbs-pill--muted"));
            badges.Add(CreateMiniBadge($"W:{buildable.workCost}", "tmbs-pill--muted"));

            if (buildable.tier > 0)
                badges.Add(CreateMiniBadge($"T{buildable.tier}", "tmbs-pill--muted"));

            if (buildable.sortOrder != 0)
                badges.Add(CreateMiniBadge($"#{buildable.sortOrder}", "tmbs-pill--muted"));

            if (buildable.tile == null)
                badges.Add(CreateMiniBadge("No Tile", "tmbs-pill--warn"));

            info.Add(badges);
            card.Add(info);
            return card;
        }

        private static Label CreateMiniBadge(string text, string pillVariantClass)
        {
            var badge = new Label(text);
            badge.AddToClassList("tmbs-pill");
            badge.AddToClassList(pillVariantClass);
            badge.style.fontSize = 9;
            badge.style.paddingLeft = 5;
            badge.style.paddingRight = 5;
            badge.style.paddingTop = 1;
            badge.style.paddingBottom = 1;
            badge.style.marginRight = 3;
            badge.style.marginBottom = 2;
            return badge;
        }

        private void RefreshDetailAll()
        {
            bool hasSelection = _selected != null;
            _detailEmptyState.style.display = hasSelection ? DisplayStyle.None : DisplayStyle.Flex;
            _detailContentScroll.style.display = hasSelection ? DisplayStyle.Flex : DisplayStyle.None;

            if (!hasSelection)
                return;

            _categoryRenameSource = _selected.GetEffectiveCategory();
            PopulateDetailFieldsWithoutNotify();
            RefreshDetailHeaderAndVisualButtons();
            RefreshDetailTaxonomyUI();
            RefreshActiveTagsList();
            RefreshFootprintSection();
        }

        private void PopulateDetailFieldsWithoutNotify()
        {
            if (_selected == null)
                return;

            _isUpdatingDetailFields = true;
            try
            {
                _detDisplayNameField.SetValueWithoutNotify(_selected.displayName ?? string.Empty);
                _detIdField.SetValueWithoutNotify(_selected.id ?? string.Empty);
                _detDescriptionField.SetValueWithoutNotify(_selected.description ?? string.Empty);

                _detCategoryField.SetValueWithoutNotify(_selected.category ?? string.Empty);
                _detSubcategoryField.SetValueWithoutNotify(_selected.subcategory ?? string.Empty);
                if (_detCategoryPopup != null && _detCategoryPopup.choices != null)
                {
                    string cat = _selected.GetEffectiveCategory();
                    if (_detCategoryPopup.choices.Contains(cat))
                        _detCategoryPopup.SetValueWithoutNotify(cat);
                }
                if (_detSubcategoryPopup != null && _detSubcategoryPopup.choices != null)
                {
                    string sub = _selected.GetEffectiveSubcategory();
                    _detSubcategoryPopup.SetValueWithoutNotify(!string.IsNullOrEmpty(sub) && _detSubcategoryPopup.choices.Contains(sub)
                        ? sub
                        : NoneSubcategoryLabel);
                }
                _detRoleField.SetValueWithoutNotify(_selected.placementRole);
                _detTierField.SetValueWithoutNotify(Mathf.Max(0, _selected.tier));
                _detSortOrderField.SetValueWithoutNotify(_selected.sortOrder);

                _detTileField.SetValueWithoutNotify(_selected.tile);
                _detSpriteField.SetValueWithoutNotify(_selected.previewSprite);

                _detWorkCostField.SetValueWithoutNotify(Mathf.Max(1, _selected.workCost));
                _detResourceCostField.SetValueWithoutNotify(Mathf.Max(0, _selected.resourceCost));
                _detIsBlueprintToggle.SetValueWithoutNotify(_selected.isBlueprint);
                _detFootprintField.SetValueWithoutNotify(new Vector2Int(
                    Mathf.Max(1, _selected.footprintSize.x),
                    Mathf.Max(1, _selected.footprintSize.y)));

                _detMaxHealthField.SetValueWithoutNotify(Mathf.Max(0, _selected.maxHealth));
                _detCanRotateToggle.SetValueWithoutNotify(_selected.canRotate);
                _detCanMirrorToggle.SetValueWithoutNotify(_selected.canMirror);
                _detBlocksMovementToggle.SetValueWithoutNotify(_selected.blocksMovement);
            }
            finally
            {
                _isUpdatingDetailFields = false;
            }
        }

        private void RefreshDetailHeaderAndVisualButtons()
        {
            if (_selected == null)
                return;

            _detailTitleLabel.text = _selected.GetEffectiveDisplayName();
            string assetPath = AssetDatabase.GetAssetPath(_selected);
            _detailAssetPathLabel.text = string.IsNullOrEmpty(assetPath) ? "(Unsaved)" : assetPath;
            _detailCatDot.style.backgroundColor = ResolveCategoryColor(_selected.GetEffectiveCategory());

            _detailMissingTileBanner.style.display = _selected.tile == null ? DisplayStyle.Flex : DisplayStyle.None;
            _detCreateTileBtn.SetEnabled(_selected.previewSprite != null);
            _detExtractSpriteBtn.SetEnabled(_selected.tile != null);
            RefreshDefaultBuildableIndicator();
            _detailThumb.MarkDirtyRepaint();
        }

        private void RefreshDefaultBuildableIndicator()
        {
            if (_setDefaultBuildableBtn == null || _defaultBuildableBadge == null)
                return;

            bool hasConfig = _targetConfig != null;
            bool isDefault = hasConfig && _selected != null && _targetConfig.defaultBuildable == _selected;

            _setDefaultBuildableBtn.SetEnabled(hasConfig && _selected != null);
            _setDefaultBuildableBtn.text = isDefault ? "Default ✓" : "Set Default";
            _defaultBuildableBadge.style.display = isDefault ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ToggleSelectedAsDefaultBuildable()
        {
            if (_targetConfig == null || _selected == null)
                return;

            bool clearing = _targetConfig.defaultBuildable == _selected;
            Undo.RecordObject(_targetConfig, clearing ? "Clear Default Buildable" : "Set Default Buildable");
            _targetConfig.defaultBuildable = clearing ? null : _selected;
            EditorUtility.SetDirty(_targetConfig);

            RefreshDefaultBuildableIndicator();
            SetStatus(clearing
                ? $"Cleared default buildable on '{_targetConfig.name}'."
                : $"Set '{_selected.GetEffectiveDisplayName()}' as default buildable on '{_targetConfig.name}'.");
        }

        private void RefreshActiveTagsList()
        {
            _detActiveTagsContainer.Clear();
            if (_selected == null)
                return;

            var list = _selected.tags;
            if (list == null || list.Count == 0)
            {
                var emptyLbl = new Label("No tags assigned");
                emptyLbl.AddToClassList("tmbs-subtitle");
                _detActiveTagsContainer.Add(emptyLbl);
                RefreshProjectTagSuggestions();
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                string tag = list[i];
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                string captured = tag.Trim();
                var chip = new Button(() => RemoveTagFromSelected(captured))
                {
                    text = $"#{captured} ×",
                    tooltip = $"Click to remove tag '{captured}'."
                };
                chip.AddToClassList("tmbs-b-tag-chip");
                chip.AddToClassList("tmbs-b-tag-chip--active");
                _detActiveTagsContainer.Add(chip);
            }

            RefreshProjectTagSuggestions();
        }

        private void RefreshFootprintSection()
        {
            if (_selected == null)
                return;

            _detBlueprintGroup.style.display = _selected.isBlueprint ? DisplayStyle.Flex : DisplayStyle.None;

            Vector2Int size = _selected.GetSize();
            _detFootprintAreaLabel.text = $"Footprint: {size.x} × {size.y} ({size.x * size.y} cell{(size.x * size.y == 1 ? string.Empty : "s")})";

            _detFootprintGrid.Clear();
            int drawCols = Mathf.Min(6, size.x);
            int drawRows = Mathf.Min(6, size.y);

            for (int y = 0; y < drawRows; y++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                for (int x = 0; x < drawCols; x++)
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("tmbs-b-footprint-cell");
                    row.Add(cell);
                }
                _detFootprintGrid.Add(row);
            }

            if (size.x > 6 || size.y > 6)
            {
                var truncLbl = new Label($"(Showing {drawCols}×{drawRows} preview of {size.x}×{size.y})");
                truncLbl.AddToClassList("tmbs-subtitle");
                truncLbl.style.marginTop = 2;
                _detFootprintGrid.Add(truncLbl);
            }
        }

        private void RefreshSummaryLabels()
        {
            _summaryCountLabel.text = $"Showing {_filteredBuildables.Count} of {_allBuildables.Count} buildables";

            if (MissingTileCount > 0)
            {
                _missingWarningBadge.style.display = DisplayStyle.Flex;
                _missingWarningBadge.text = $"⚠ {MissingTileCount} missing tile(s)";
            }
            else
            {
                _missingWarningBadge.style.display = DisplayStyle.None;
            }
        }

        private void ToggleQuickCreatePanel()
        {
            bool next = _quickCreatePanel.style.display == DisplayStyle.None;
            SetQuickCreateVisible(next);
        }

        private void SetQuickCreateVisible(bool visible)
        {
            _quickCreatePanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                if (!string.Equals(_categoryFilter, AllCategoriesLabel, StringComparison.OrdinalIgnoreCase))
                    _qcCategoryField.SetValueWithoutNotify(_categoryFilter);

                if (!string.Equals(_subcategoryFilter, AllSubcategoriesLabel, StringComparison.OrdinalIgnoreCase))
                    _qcSubcategoryField.SetValueWithoutNotify(_subcategoryFilter);

                RefreshQuickCreateTaxonomyPopups(GetAllDefinedCategories(), GetAllDefinedSubcategories());

                if (string.IsNullOrWhiteSpace(_qcFolderField.value))
                    _qcFolderField.SetValueWithoutNotify(_defaultCreateFolder);

                _qcNameField.Focus();
            }
        }

        private void BrowseQuickCreateFolder()
        {
            string startDir = _qcFolderField.value;
            if (string.IsNullOrWhiteSpace(startDir) || !startDir.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                startDir = "Assets";

            string chosen = EditorUtility.OpenFolderPanel("Select Output Folder Inside Project Assets", startDir, string.Empty);
            if (string.IsNullOrEmpty(chosen))
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            string chosenNorm = Path.GetFullPath(chosen).Replace('\\', '/');

            if (chosenNorm.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                string relative = chosenNorm.Substring(projectRoot.Length + 1);
                if (relative.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                {
                    _qcFolderField.SetValueWithoutNotify(relative);
                    _defaultCreateFolder = relative;
                    return;
                }
            }

            SetStatus("Please select a folder inside the Unity project's 'Assets/' directory.");
        }

        private void ExecuteQuickCreate()
        {
            string folder = NormalizeAssetFolder(_qcFolderField.value, _defaultCreateFolder);
            EnsureAssetFolder(folder);

            string rawName = string.IsNullOrWhiteSpace(_qcNameField.value) ? "New Buildable" : _qcNameField.value.Trim();
            string cat = string.IsNullOrWhiteSpace(_qcCategoryField.value) ? "General" : _qcCategoryField.value.Trim();
            string sub = string.IsNullOrWhiteSpace(_qcSubcategoryField?.value) ? string.Empty : _qcSubcategoryField.value.Trim();
            var role = _qcRoleField.value is TmbsPlacementRole r ? r : TmbsPlacementRole.Structure;
            var tile = _qcTileField.value as TileBase;
            var sprite = _qcSpriteField.value as Sprite;

            bool taxonomyChanged = false;
            if (!string.Equals(cat, "General", StringComparison.OrdinalIgnoreCase) && _customCategories.Add(cat))
                taxonomyChanged = true;
            if (!string.IsNullOrEmpty(sub) && _customSubcategories.Add(sub))
                taxonomyChanged = true;
            if (taxonomyChanged)
                SaveCustomTaxonomy();

            if (tile == null && sprite != null && _qcAutoTileToggle.value)
            {
                string tilesFolder = folder + "/Tiles";
                EnsureAssetFolder(tilesFolder);
                tile = CreateTileAssetFromSprite(sprite, tilesFolder, rawName);
            }

            var created = CreateBuildableAsset(rawName, cat, role, tile, sprite, folder, sub);
            AssetDatabase.SaveAssets();

            SetQuickCreateVisible(false);
            _qcNameField.SetValueWithoutNotify("New Buildable");
            _qcTileField.SetValueWithoutNotify(null);
            _qcSpriteField.SetValueWithoutNotify(null);

            _defaultCreateFolder = folder;
            SetStatus($"Created '{created.GetEffectiveDisplayName()}' in {folder}.");
            SelectBuildable(created);
        }

        private void CreateFromSelectedProjectAssets()
        {
            var objects = Selection.objects;
            if (objects == null || objects.Length == 0)
            {
                SetStatus("Select Sprite, Texture2D, or TileBase assets in the Project window first.");
                return;
            }

            string folder = NormalizeAssetFolder(
                _qcFolderField != null ? _qcFolderField.value : null,
                _defaultCreateFolder);
            EnsureAssetFolder(folder);

            string defaultCat = !string.Equals(_categoryFilter, AllCategoriesLabel, StringComparison.OrdinalIgnoreCase)
                ? _categoryFilter
                : "General";

            var tilesFolder = folder + "/Tiles";
            var createdAssets = new List<TmbsBuildableData>();
            var processed = new HashSet<UnityEngine.Object>();

            for (int i = 0; i < objects.Length; i++)
            {
                UnityEngine.Object obj = objects[i];
                if (obj == null || !processed.Add(obj))
                    continue;

                if (obj is TileBase tile)
                {
                    Sprite resolved = TmbsPaletteSlot.ResolveSprite(null, tile);
                    string niceName = ObjectNames.NicifyVariableName(tile.name);
                    createdAssets.Add(CreateBuildableAsset(niceName, defaultCat, TmbsPlacementRole.Structure, tile, resolved, folder));
                }
                else
                {
                    Sprite sprite = obj as Sprite;
                    if (sprite == null && obj is Texture2D)
                    {
                        string texPath = AssetDatabase.GetAssetPath(obj);
                        if (!string.IsNullOrEmpty(texPath))
                            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
                    }

                    if (sprite != null && processed.Add(sprite))
                    {
                        EnsureAssetFolder(tilesFolder);
                        string niceName = ObjectNames.NicifyVariableName(sprite.name);
                        Tile createdTile = CreateTileAssetFromSprite(sprite, tilesFolder, sprite.name);
                        createdAssets.Add(CreateBuildableAsset(niceName, defaultCat, TmbsPlacementRole.Structure, createdTile, sprite, folder));
                    }
                }
            }

            if (createdAssets.Count == 0)
            {
                SetStatus("No Sprite, Texture2D (Sprite), or TileBase found in the current Project selection.");
                return;
            }

            AssetDatabase.SaveAssets();
            SetStatus($"Created {createdAssets.Count} buildable asset(s) from selection.");
            SelectBuildable(createdAssets[0]);
        }

        private TmbsBuildableData CreateBuildableAsset(
            string displayName,
            string category,
            TmbsPlacementRole role,
            TileBase tile,
            Sprite previewSprite,
            string folder,
            string subcategory = "")
        {
            var asset = ScriptableObject.CreateInstance<TmbsBuildableData>();
            asset.displayName = string.IsNullOrWhiteSpace(displayName) ? "New Buildable" : displayName.Trim();
            asset.id = Slugify(asset.displayName);
            asset.category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
            asset.subcategory = string.IsNullOrWhiteSpace(subcategory) ? string.Empty : subcategory.Trim();
            asset.placementRole = role;
            asset.tile = tile;
            asset.previewSprite = previewSprite != null ? previewSprite : TmbsPaletteSlot.ResolveSprite(null, tile);
            asset.sortOrder = GetNextSortOrder();

            string safeFileName = SanitizeFileName(asset.displayName);
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeFileName}.asset");
            AssetDatabase.CreateAsset(asset, uniquePath);
            return asset;
        }

        private static Tile CreateTileAssetFromSprite(Sprite sprite, string targetFolder, string preferredName)
        {
            EnsureAssetFolder(targetFolder);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;

            string safeBase = SanitizeFileName(string.IsNullOrWhiteSpace(preferredName) ? sprite.name : preferredName);
            string tilePath = AssetDatabase.GenerateUniqueAssetPath($"{targetFolder}/T_{safeBase}.asset");
            AssetDatabase.CreateAsset(tile, tilePath);
            return tile;
        }

        private void CreateTileFromSelectedSprite()
        {
            if (_selected == null || _selected.previewSprite == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(_selected);
            string baseDir = !string.IsNullOrEmpty(assetPath)
                ? Path.GetDirectoryName(assetPath)?.Replace('\\', '/')
                : _defaultCreateFolder;
            string tilesDir = NormalizeAssetFolder(baseDir, _defaultCreateFolder) + "/Tiles";

            Tile createdTile = CreateTileAssetFromSprite(_selected.previewSprite, tilesDir, _selected.GetEffectiveDisplayName());
            AssetDatabase.SaveAssets();

            ApplyDetailMutation("Create Tile from Sprite", b => b.tile = createdTile,
                syncDetailFields: true, updateHeader: true, refreshListAndSort: true, refreshFilters: true);

            SetStatus($"Created Tile asset '{createdTile.name}' and assigned it to '{_selected.GetEffectiveDisplayName()}'.");
        }

        private void ExtractSpriteFromSelectedTile()
        {
            if (_selected == null || _selected.tile == null)
                return;

            Sprite sprite = TmbsPaletteSlot.ResolveSprite(null, _selected.tile);
            if (sprite == null)
            {
                SetStatus($"Could not extract a Sprite from Tile '{_selected.tile.name}'.");
                return;
            }

            ApplyDetailMutation("Extract Sprite from Tile", b => b.previewSprite = sprite,
                syncDetailFields: true, updateHeader: true, refreshListAndSort: true);
            SetStatus($"Assigned preview sprite '{sprite.name}'.");
        }

        private void SyncToSceneUI()
        {
#if UNITY_2023_1_OR_NEWER
            var existingUi = UnityEngine.Object.FindAnyObjectByType<TmbsBuildingUI>();
#else
            var existingUi = UnityEngine.Object.FindObjectOfType<TmbsBuildingUI>();
#endif
            if (existingUi != null)
            {
                var foundBuildables = new List<TmbsBuildableData>();
                for (int i = 0; i < _allBuildables.Count; i++)
                {
                    var asset = _allBuildables[i];
                    if (asset != null && asset.tile != null && !foundBuildables.Contains(asset))
                        foundBuildables.Add(asset);
                }

                foundBuildables.Sort((a, b) => TmbsBuildableData.Compare(a, b, _sortMode, _sortDescending));

                Undo.RecordObject(existingUi, "Sync Buildables to Scene UI");
                var so = new SerializedObject(existingUi);
                var buildablesProp = so.FindProperty("buildables");
                if (buildablesProp != null)
                {
                    buildablesProp.ClearArray();
                    for (int i = 0; i < foundBuildables.Count; i++)
                    {
                        buildablesProp.InsertArrayElementAtIndex(i);
                        buildablesProp.GetArrayElementAtIndex(i).objectReferenceValue = foundBuildables[i];
                    }
                    so.ApplyModifiedProperties();
                }

                EditorUtility.SetDirty(existingUi);
                if (Application.isPlaying)
                {
                    existingUi.BuildCategoryTabs();
                    existingUi.BuildPalette();
                }

                string dirLabel = _sortDescending ? "Desc" : "Asc";
                SetStatus($"Updated Scene UI ({foundBuildables.Count} buildables, sorted by {_sortMode} {dirLabel}) without destroying layout.");
                return;
            }

            GameObject canvas = TMBS.Editor.TmbsBuildingUIBuilder.CreateOrUpdateSceneUI();
            if (canvas != null)
                SetStatus("Created Scene UI catalog using TmbsBuildingUIBuilder.");
            else
                SetStatus("Could not auto-configure Scene UI (check Console or add a Grid/Tilemap first).");
        }

        private void AutoGenerateIdForSelected()
        {
            if (_selected == null)
                return;

            string slug = Slugify(_selected.GetEffectiveDisplayName());
            ApplyDetailMutation("Auto-Generate Buildable ID", b => b.id = slug,
                syncDetailFields: true, refreshListAndSort: true);
        }

        private void ApplyCategoryPreset(string category)
        {
            string normalized = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
            _categoryRenameSource = normalized;
            ApplyDetailMutation("Set Buildable Category", b => b.category = normalized,
                syncDetailFields: true, updateHeader: true, refreshListAndSort: true, refreshFilters: true);
        }

        private void ApplySubcategoryPreset(string subcategory)
        {
            string normalized = string.IsNullOrWhiteSpace(subcategory) ? string.Empty : subcategory.Trim();
            ApplyDetailMutation("Set Buildable Subcategory", b => b.subcategory = normalized,
                syncDetailFields: true, refreshListAndSort: true, refreshFilters: true);
        }

        private void ToggleInlineAddCategory()
        {
            if (_detNewCategoryRow == null)
                return;

            bool show = _detNewCategoryRow.style.display == DisplayStyle.None;
            _detNewCategoryRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                _detNewCategoryInput.SetValueWithoutNotify(string.Empty);
                _detNewCategoryInput.Focus();
            }
        }

        private void AddAndAssignCategoryFromInput()
        {
            string candidate = (_detNewCategoryInput?.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(candidate))
                return;

            _customCategories.Add(candidate);
            SaveCustomTaxonomy();

            _detNewCategoryInput.SetValueWithoutNotify(string.Empty);
            _detNewCategoryRow.style.display = DisplayStyle.None;

            if (_selected != null)
            {
                ApplyCategoryPreset(candidate);
                SetStatus($"Added category '{candidate}' and assigned it to '{_selected.GetEffectiveDisplayName()}'.");
            }
            else
            {
                RebuildDynamicFilterChoices();
                SetStatus($"Added category '{candidate}'.");
            }
        }

        private void RenameSelectedCategoryAcrossProject()
        {
            if (_selected == null)
                return;

            string oldCat = string.IsNullOrWhiteSpace(_categoryRenameSource) ? "General" : _categoryRenameSource.Trim();
            string newCat = string.IsNullOrWhiteSpace(_detCategoryField.value) ? "General" : _detCategoryField.value.Trim();
            if (string.Equals(oldCat, newCat, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus($"Category is already '{newCat}'. Edit the text field first, then click Rename All.");
                return;
            }

            var targets = new List<UnityEngine.Object>();
            for (int i = 0; i < _allBuildables.Count; i++)
            {
                var b = _allBuildables[i];
                if (b != null && string.Equals(b.GetEffectiveCategory(), oldCat, StringComparison.OrdinalIgnoreCase))
                    targets.Add(b);
            }

            if (targets.Count > 0)
            {
                Undo.RecordObjects(targets.ToArray(), $"Rename Category '{oldCat}' to '{newCat}'");
                for (int i = 0; i < targets.Count; i++)
                {
                    var b = (TmbsBuildableData)targets[i];
                    b.category = newCat;
                    EditorUtility.SetDirty(b);
                }
            }

            if (_customCategories.Remove(oldCat))
                _customCategories.Add(newCat);
            else if (!string.Equals(newCat, "General", StringComparison.OrdinalIgnoreCase))
                _customCategories.Add(newCat);
            SaveCustomTaxonomy();

            if (string.Equals(_categoryFilter, oldCat, StringComparison.OrdinalIgnoreCase))
                _categoryFilter = newCat;

            _categoryRenameSource = newCat;
            SetStatus($"Renamed category '{oldCat}' -> '{newCat}' across {targets.Count} asset(s).");
            Refresh();
        }

        private void RemoveUnusedCustomCategory(string category)
        {
            if (_customCategories.Remove(category))
            {
                SaveCustomTaxonomy();
                RebuildDynamicFilterChoices();
                SetStatus($"Removed unused custom category '{category}'.");
            }
        }

        private void ToggleInlineAddSubcategory()
        {
            if (_detNewSubcategoryRow == null)
                return;

            bool show = _detNewSubcategoryRow.style.display == DisplayStyle.None;
            _detNewSubcategoryRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                _detNewSubcategoryInput.SetValueWithoutNotify(string.Empty);
                _detNewSubcategoryInput.Focus();
            }
        }

        private void AddAndAssignSubcategoryFromInput()
        {
            string candidate = (_detNewSubcategoryInput?.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(candidate))
                return;

            _customSubcategories.Add(candidate);
            SaveCustomTaxonomy();

            _detNewSubcategoryInput.SetValueWithoutNotify(string.Empty);
            _detNewSubcategoryRow.style.display = DisplayStyle.None;

            if (_selected != null)
            {
                ApplySubcategoryPreset(candidate);
                SetStatus($"Added subcategory '{candidate}' and assigned it to '{_selected.GetEffectiveDisplayName()}'.");
            }
            else
            {
                RebuildDynamicFilterChoices();
                SetStatus($"Added subcategory '{candidate}'.");
            }
        }

        private void ClearSelectedSubcategory()
        {
            ApplySubcategoryPreset(string.Empty);
        }

        private void RemoveUnusedCustomSubcategory(string subcategory)
        {
            if (_customSubcategories.Remove(subcategory))
            {
                SaveCustomTaxonomy();
                RebuildDynamicFilterChoices();
                SetStatus($"Removed unused custom subcategory '{subcategory}'.");
            }
        }

        private void LoadCustomTaxonomy()
        {
            LoadStringSet(CustomCategoriesPrefKey, _customCategories);
            LoadStringSet(CustomSubcategoriesPrefKey, _customSubcategories);
        }

        private void SaveCustomTaxonomy()
        {
            SaveStringSet(CustomCategoriesPrefKey, _customCategories);
            SaveStringSet(CustomSubcategoriesPrefKey, _customSubcategories);
        }

        private static void LoadStringSet(string prefKey, SortedSet<string> target)
        {
            target.Clear();
            string raw = EditorUserSettings.GetConfigValue(prefKey);
            if (string.IsNullOrEmpty(raw))
                return;

            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;
                string decoded = Uri.UnescapeDataString(parts[i]).Trim();
                if (!string.IsNullOrEmpty(decoded))
                    target.Add(decoded);
            }
        }

        private static void SaveStringSet(string prefKey, SortedSet<string> values)
        {
            string serialized = values == null || values.Count == 0
                ? string.Empty
                : string.Join("|", values.Select(Uri.EscapeDataString));
            EditorUserSettings.SetConfigValue(prefKey, serialized);
        }

        private void ApplyFootprintPreset(Vector2Int size)
        {
            ApplyDetailMutation("Set Footprint Size", b =>
            {
                b.isBlueprint = true;
                b.footprintSize = size;
            },
            syncDetailFields: true, updateFootprint: true, refreshListAndSort: true, refreshFilters: true);
        }

        private void AddTagFromInput()
        {
            if (_selected == null || _detNewTagField == null)
                return;

            string candidate = (_detNewTagField.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(candidate))
                return;

            _detNewTagField.SetValueWithoutNotify(string.Empty);
            if (_selected.HasTag(candidate))
                return;

            ApplyDetailMutation("Add Buildable Tag", b =>
            {
                if (b.tags == null)
                    b.tags = new List<string>();
                b.tags.Add(candidate);
            },
            refreshTags: true, refreshFilters: true);
        }

        private void RemoveTagFromSelected(string tag)
        {
            if (_selected == null || _selected.tags == null)
                return;

            ApplyDetailMutation("Remove Buildable Tag", b =>
            {
                b.tags.RemoveAll(t => string.Equals(t?.Trim(), tag, StringComparison.OrdinalIgnoreCase));
            },
            refreshTags: true, refreshFilters: true);
        }

        private void ToggleTagOnSelected(string tag)
        {
            if (_selected == null)
                return;

            if (_selected.HasTag(tag))
            {
                RemoveTagFromSelected(tag);
                return;
            }

            ApplyDetailMutation("Add Buildable Tag", b =>
            {
                if (b.tags == null)
                    b.tags = new List<string>();
                b.tags.Add(tag);
            },
            refreshTags: true, refreshFilters: true);
        }

        private void PingSelectedAsset()
        {
            if (_selected == null)
                return;

            Selection.activeObject = _selected;
            EditorGUIUtility.PingObject(_selected);
        }

        private void DuplicateSelectedAsset()
        {
            if (_selected == null)
                return;

            string srcPath = AssetDatabase.GetAssetPath(_selected);
            if (string.IsNullOrEmpty(srcPath))
                return;

            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(srcPath);
            if (!AssetDatabase.CopyAsset(srcPath, uniquePath))
                return;

            AssetDatabase.SaveAssets();
            var copy = AssetDatabase.LoadAssetAtPath<TmbsBuildableData>(uniquePath);
            if (copy != null)
            {
                copy.displayName = _selected.GetEffectiveDisplayName() + " Copy";
                copy.id = Slugify(copy.displayName);
                copy.sortOrder = GetNextSortOrder();
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssets();
                SetStatus($"Duplicated '{_selected.GetEffectiveDisplayName()}'.");
                SelectBuildable(copy);
            }
        }

        private void DeleteSelectedAsset()
        {
            if (_selected == null)
                return;

            string path = AssetDatabase.GetAssetPath(_selected);
            if (string.IsNullOrEmpty(path))
                return;

            if (!EditorUtility.DisplayDialog(
                "Delete Buildable",
                $"Delete '{_selected.GetEffectiveDisplayName()}'?\n\nAsset: {path}\nThis cannot be undone.",
                "Delete",
                "Cancel"))
            {
                return;
            }

            string deletedName = _selected.GetEffectiveDisplayName();
            int currentIdx = _filteredBuildables.IndexOf(_selected);
            TmbsBuildableData nextSelection = null;
            if (currentIdx >= 0)
            {
                if (currentIdx + 1 < _filteredBuildables.Count)
                    nextSelection = _filteredBuildables[currentIdx + 1];
                else if (currentIdx - 1 >= 0)
                    nextSelection = _filteredBuildables[currentIdx - 1];
            }

            if (AssetDatabase.DeleteAsset(path))
            {
                _selected = nextSelection;
                SetStatus($"Deleted '{deletedName}'.");
                Refresh();
            }
        }

        private int GetNextSortOrder()
        {
            int max = 0;
            bool anyNonZero = false;
            for (int i = 0; i < _allBuildables.Count; i++)
            {
                if (_allBuildables[i].sortOrder > max)
                    max = _allBuildables[i].sortOrder;
                if (_allBuildables[i].sortOrder != 0)
                    anyNonZero = true;
            }

            return anyNonZero ? max + 10 : 0;
        }

        private void UpdateSortDirButtonText()
        {
            _sortDirBtn.text = _sortDescending ? "↓ Desc" : "↑ Asc";
        }

        private void UpdateViewModeButtonText()
        {
            _viewModeBtn.text = _gridView ? "⊞ Grid" : "☰ List";
        }

        private void SetStatus(string message)
        {
            if (_statusBannerLabel != null)
                _statusBannerLabel.text = message ?? string.Empty;
        }

        private static Color ResolveCategoryColor(string category)
        {
#if UNITY_2023_1_OR_NEWER
            var sceneUi = UnityEngine.Object.FindAnyObjectByType<TmbsBuildingUI>();
#else
            var sceneUi = UnityEngine.Object.FindObjectOfType<TmbsBuildingUI>();
#endif
            return sceneUi != null
                ? sceneUi.Theme.ResolveCategoryColor(category)
                : FallbackTheme.ResolveCategoryColor(category);
        }

        private static void DrawThumbnail(TmbsBuildableData buildable, IMGUIContainer container)
        {
            if (buildable == null || container == null)
                return;

            Rect content = container.contentRect;
            if (content.width < 4f || content.height < 4f)
                return;

            Rect drawRect = new Rect(3f, 3f, content.width - 6f, content.height - 6f);
            Sprite sprite = TmbsPaletteSlot.ResolveSprite(buildable, buildable.tile);

            if (sprite != null && sprite.texture != null && sprite.textureRect.width > 0f && sprite.textureRect.height > 0f)
            {
                Texture2D tex = sprite.texture;
                Rect tr = sprite.textureRect;
                Rect uv = new Rect(
                    tr.x / tex.width,
                    tr.y / tex.height,
                    tr.width / tex.width,
                    tr.height / tex.height);

                Rect fitted = FitAspectRect(drawRect, tr.width / tr.height);
                GUI.DrawTextureWithTexCoords(fitted, tex, uv, true);
                return;
            }

            if (buildable.tile != null)
            {
                Texture2D previewTex = AssetPreview.GetAssetPreview(buildable.tile) ?? AssetPreview.GetMiniThumbnail(buildable.tile);
                if (previewTex != null)
                {
                    GUI.DrawTexture(drawRect, previewTex, ScaleMode.ScaleToFit, true);
                    return;
                }
            }

            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 9,
                wordWrap = true
            };
            GUI.Label(drawRect, buildable.tile == null ? "NO\nTILE" : "NO\nIMG", style);
        }

        private static Rect FitAspectRect(Rect bounds, float aspect)
        {
            if (aspect <= 0f || bounds.width <= 0f || bounds.height <= 0f)
                return bounds;

            float rectAspect = bounds.width / bounds.height;
            if (aspect > rectAspect)
            {
                float h = bounds.width / aspect;
                return new Rect(bounds.x, bounds.y + (bounds.height - h) * 0.5f, bounds.width, h);
            }

            float w = bounds.height * aspect;
            return new Rect(bounds.x + (bounds.width - w) * 0.5f, bounds.y, w, bounds.height);
        }

        private static string NormalizeAssetFolder(string raw, string fallback)
        {
            string candidate = string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
            candidate = candidate.Replace('\\', '/').TrimEnd('/');
            if (!candidate.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                return "Assets/TMBS/Buildables";
            return candidate;
        }

        private static void EnsureAssetFolder(string assetFolderPath)
        {
            string norm = NormalizeAssetFolder(assetFolderPath, "Assets/TMBS/Buildables");
            if (AssetDatabase.IsValidFolder(norm))
                return;

            string[] parts = norm.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "new_buildable";

            var sb = new StringBuilder(value.Length);
            bool prevUnderscore = true;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    prevUnderscore = false;
                }
                else if (!prevUnderscore)
                {
                    sb.Append('_');
                    prevUnderscore = true;
                }
            }

            string result = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "new_buildable" : result;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NewBuildable";

            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isInvalid = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        isInvalid = true;
                        break;
                    }
                }
                sb.Append(isInvalid ? '_' : c);
            }

            string trimmed = sb.ToString().Trim();
            return string.IsNullOrEmpty(trimmed) ? "NewBuildable" : trimmed;
        }
    }
}
