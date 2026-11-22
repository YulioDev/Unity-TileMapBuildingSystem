using System;
using TMBS.Runtime.Config;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMBS.Editor.InputConfigurator
{
    public sealed class TmbsInputConfiguratorWindow : EditorWindow
    {
        private enum TabSection
        {
            AllSetup = 0,
            Mode = 1,
            Controls = 2,
            LiveTest = 3
        }

        private TmbsRootConfig _targetConfig;
        private TabSection _activeTab = TabSection.AllSetup;

        private ObjectField _configField;
        private Button _pingConfigBtn;
        private Button _navAllBtn;
        private Button _navModeBtn;
        private Button _navControlsBtn;
        private Button _navLiveBtn;

        private VisualElement _noConfigBanner;
        private ModeCardsView _modeCardsView;
        private ControlsView _controlsView;
        private LiveTesterView _liveTesterView;
        private ScrollView _contentScroll;

        private Label _footerBoundLabel;
        private Label _footerConflictLabel;
        private Label _footerModeLabel;
        private Button _revertBtn;

        [MenuItem("Window/TMBS/Input Configurator")]
        public static void ShowWindow()
        {
            Open(null);
        }

        public static void Open(TmbsRootConfig targetConfig = null)
        {
            var window = GetWindow<TmbsInputConfiguratorWindow>("TMBS Input");
            window.minSize = new Vector2(880, 560);
            if (targetConfig != null)
                window.SetTargetConfig(targetConfig);
            else if (window._targetConfig == null)
                window.AutoDiscoverConfig();
            window.Show();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _liveTesterView?.Detach();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("tmbs-root");
            if (!EditorGUIUtility.isProSkin)
                root.AddToClassList("tmbs-light");

            LoadStyleSheet(root);

            BuildHeader(root);

            var body = new VisualElement();
            body.AddToClassList("tmbs-body");
            root.Add(body);

            BuildNavRail(body);

            _contentScroll = new ScrollView();
            _contentScroll.AddToClassList("tmbs-content-scroll");
            body.Add(_contentScroll);

            BuildNoConfigBanner();
            _contentScroll.Add(_noConfigBanner);

            _modeCardsView = new ModeCardsView();
            _modeCardsView.ModeSelected += OnModeSelected;
            _modeCardsView.JumpToControlsRequested += () => SetActiveTab(TabSection.Controls);
            _contentScroll.Add(_modeCardsView);

            _controlsView = new ControlsView();
            _controlsView.ConfigMutationRequested += MutateConfig;
            _controlsView.QuickSwitchModeRequested += OnModeSelected;
            _contentScroll.Add(_controlsView);

            _liveTesterView = new LiveTesterView();
            _contentScroll.Add(_liveTesterView);

            BuildFooter(root);

            if (_targetConfig == null)
                AutoDiscoverConfig();

            RefreshAll();
            SetActiveTab(_activeTab);

            root.schedule.Execute(() => _liveTesterView?.TickUI()).Every(100);
        }

        private static void LoadStyleSheet(VisualElement root)
        {
            var guids = AssetDatabase.FindAssets("TmbsInputConfigurator t:StyleSheet");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null)
                {
                    root.styleSheets.Add(sheet);
                    return;
                }
            }
        }

        private void BuildHeader(VisualElement root)
        {
            var header = new VisualElement();
            header.AddToClassList("tmbs-header");
            root.Add(header);

            var titleGroup = new VisualElement();
            titleGroup.AddToClassList("tmbs-title-group");
            header.Add(titleGroup);

            var title = new Label("TMBS Input Configurator");
            title.AddToClassList("tmbs-title");
            titleGroup.Add(title);

            var sub = new Label("Input & Binding Settings");
            sub.AddToClassList("tmbs-subtitle");
            titleGroup.Add(sub);

            var picker = new VisualElement();
            picker.AddToClassList("tmbs-config-picker");
            header.Add(picker);

            _configField = new ObjectField("Config:")
            {
                objectType = typeof(TmbsRootConfig),
                allowSceneObjects = false
            };
            _configField.AddToClassList("tmbs-config-field");
            _configField.RegisterValueChangedCallback(evt =>
            {
                SetTargetConfig(evt.newValue as TmbsRootConfig);
            });
            picker.Add(_configField);

            _pingConfigBtn = new Button(() =>
            {
                if (_targetConfig != null)
                    EditorGUIUtility.PingObject(_targetConfig);
            })
            {
                text = "Ping"
            };
            picker.Add(_pingConfigBtn);
        }

        private void BuildNavRail(VisualElement body)
        {
            var rail = new VisualElement();
            rail.AddToClassList("tmbs-nav-rail");
            body.Add(rail);

            _navAllBtn = CreateNavButton("Overview", TabSection.AllSetup);
            _navModeBtn = CreateNavButton("1. Mode", TabSection.Mode);
            _navControlsBtn = CreateNavButton("2. Controls", TabSection.Controls);
            _navLiveBtn = CreateNavButton("3. Live Test", TabSection.LiveTest);

            rail.Add(_navAllBtn);
            rail.Add(_navModeBtn);
            rail.Add(_navControlsBtn);
            rail.Add(_navLiveBtn);
        }

        private Button CreateNavButton(string label, TabSection section)
        {
            var btn = new Button(() => SetActiveTab(section)) { text = label };
            btn.AddToClassList("tmbs-nav-btn");
            return btn;
        }

        private void BuildNoConfigBanner()
        {
            _noConfigBanner = new VisualElement();
            _noConfigBanner.AddToClassList("tmbs-card-panel");
            _noConfigBanner.style.display = DisplayStyle.None;

            var lbl = new Label("No TmbsRootConfig asset selected. Pick one above or create a new configuration asset.");
            lbl.style.marginBottom = 8;
            _noConfigBanner.Add(lbl);

            var createBtn = new Button(CreateNewRootConfig)
            {
                text = "Create New TmbsRootConfig Asset"
            };
            createBtn.AddToClassList("tmbs-primary-btn");
            createBtn.style.alignSelf = Align.FlexStart;
            _noConfigBanner.Add(createBtn);
        }

        private void BuildFooter(VisualElement root)
        {
            var footer = new VisualElement();
            footer.AddToClassList("tmbs-footer");
            root.Add(footer);

            var left = new VisualElement();
            left.AddToClassList("tmbs-footer-status");
            footer.Add(left);

            _footerBoundLabel = new Label("—");
            _footerBoundLabel.AddToClassList("tmbs-pill");
            _footerBoundLabel.style.marginRight = 6;
            left.Add(_footerBoundLabel);

            _footerConflictLabel = new Label("—");
            _footerConflictLabel.AddToClassList("tmbs-pill");
            _footerConflictLabel.style.marginRight = 6;
            left.Add(_footerConflictLabel);

            _footerModeLabel = new Label("Mode: —");
            _footerModeLabel.AddToClassList("tmbs-pill");
            _footerModeLabel.AddToClassList("tmbs-pill--muted");
            left.Add(_footerModeLabel);

            _revertBtn = new Button(OnRevertClicked)
            {
                text = "Reset Legacy Defaults"
            };
            footer.Add(_revertBtn);
        }

        private void SetActiveTab(TabSection tab)
        {
            _activeTab = tab;

            _navAllBtn?.EnableInClassList("tmbs-nav-btn--active", tab == TabSection.AllSetup);
            _navModeBtn?.EnableInClassList("tmbs-nav-btn--active", tab == TabSection.Mode);
            _navControlsBtn?.EnableInClassList("tmbs-nav-btn--active", tab == TabSection.Controls);
            _navLiveBtn?.EnableInClassList("tmbs-nav-btn--active", tab == TabSection.LiveTest);

            UpdateSectionVisibility();
        }

        private void UpdateSectionVisibility()
        {
            if (_modeCardsView == null || _controlsView == null || _liveTesterView == null)
                return;

            bool hasConfig = _targetConfig != null;
            _noConfigBanner.style.display = (!hasConfig && _activeTab != TabSection.LiveTest)
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            bool showMode = hasConfig && (_activeTab == TabSection.AllSetup || _activeTab == TabSection.Mode);
            bool showControls = hasConfig && (_activeTab == TabSection.AllSetup || _activeTab == TabSection.Controls);
            bool showLive = _activeTab == TabSection.LiveTest;

            _modeCardsView.style.display = showMode ? DisplayStyle.Flex : DisplayStyle.None;
            _controlsView.style.display = showControls ? DisplayStyle.Flex : DisplayStyle.None;
            _liveTesterView.style.display = showLive ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetTargetConfig(TmbsRootConfig config)
        {
            _targetConfig = config;
            _controlsView?.CancelAssignment();
            RefreshAll();
        }

        private void AutoDiscoverConfig()
        {
            var guids = AssetDatabase.FindAssets("t:TmbsRootConfig");
            if (guids.Length == 0)
                return;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith("/root.asset", StringComparison.OrdinalIgnoreCase))
                {
                    _targetConfig = AssetDatabase.LoadAssetAtPath<TmbsRootConfig>(path);
                    return;
                }
            }

            string firstPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            _targetConfig = AssetDatabase.LoadAssetAtPath<TmbsRootConfig>(firstPath);
        }

        private void CreateNewRootConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create TmbsRootConfig",
                "TmbsRootConfig",
                "asset",
                "Choose where to save the new TmbsRootConfig asset.");
            if (string.IsNullOrEmpty(path))
                return;

            var cfg = CreateInstance<TmbsRootConfig>();
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();
            SetTargetConfig(cfg);
            EditorGUIUtility.PingObject(cfg);
        }

        private void OnModeSelected(TmbsInputMode mode)
        {
            MutateConfig($"Set TMBS Input Mode to {mode}", cfg =>
            {
                cfg.input.mode = mode;
            });
        }

        private void OnRevertClicked()
        {
            MutateConfig("Reset Legacy bindings to defaults", cfg =>
            {
                cfg.input.GetLegacyBindings().CopyFrom(TmbsLegacyBindings.CreateDefault());
            });
        }

        private void MutateConfig(string undoLabel, Action<TmbsRootConfig> mutator)
        {
            if (_targetConfig == null || mutator == null)
                return;

            if (_targetConfig.input == null)
                _targetConfig.input = new TmbsInputConfig();

            Undo.RecordObject(_targetConfig, undoLabel);
            mutator(_targetConfig);
            EditorUtility.SetDirty(_targetConfig);
            RefreshAll();
        }

        private void OnUndoRedoPerformed()
        {
            RefreshAll();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            _liveTesterView?.RefreshPlayModeState(_targetConfig);
        }

        private void RefreshAll()
        {
            if (_configField != null)
                _configField.SetValueWithoutNotify(_targetConfig);

            _pingConfigBtn?.SetEnabled(_targetConfig != null);

            UpdateSectionVisibility();
            _modeCardsView?.Refresh(_targetConfig);
            _controlsView?.Refresh(_targetConfig);
            _liveTesterView?.RefreshPlayModeState(_targetConfig);
            RefreshFooter();
        }

        private void RefreshFooter()
        {
            if (_footerBoundLabel == null)
                return;

            _footerBoundLabel.RemoveFromClassList("tmbs-pill--ok");
            _footerBoundLabel.RemoveFromClassList("tmbs-pill--warn");
            _footerBoundLabel.RemoveFromClassList("tmbs-pill--muted");

            _footerConflictLabel.RemoveFromClassList("tmbs-pill--ok");
            _footerConflictLabel.RemoveFromClassList("tmbs-pill--bad");
            _footerConflictLabel.RemoveFromClassList("tmbs-pill--muted");

            if (_targetConfig == null || _targetConfig.input == null)
            {
                _footerBoundLabel.text = "No config";
                _footerBoundLabel.AddToClassList("tmbs-pill--muted");
                _footerConflictLabel.text = "—";
                _footerConflictLabel.AddToClassList("tmbs-pill--muted");
                _footerModeLabel.text = "Mode: —";
                _revertBtn.SetEnabled(false);
                return;
            }

            var input = _targetConfig.input;
            _footerModeLabel.text = $"Mode: {input.mode}";
            _revertBtn.SetEnabled(input.mode == TmbsInputMode.Legacy);

            if (input.mode == TmbsInputMode.Legacy)
            {
                var legacy = input.GetLegacyBindings();
                int bound = legacy.CountBoundActions();
                int conflicts = legacy.CountConflictingSlots();

                _footerBoundLabel.text = $"✔ {bound}/7 actions bound";
                _footerBoundLabel.AddToClassList(bound == 7 ? "tmbs-pill--ok" : "tmbs-pill--warn");

                _footerConflictLabel.text = conflicts == 0
                    ? "✔ 0 conflicts"
                    : $"⚠ {conflicts} conflicting slot(s)";
                _footerConflictLabel.AddToClassList(conflicts == 0 ? "tmbs-pill--ok" : "tmbs-pill--bad");
            }
            else if (input.mode == TmbsInputMode.InputActions)
            {
#if ENABLE_INPUT_SYSTEM
                int assigned = 0;
                if (input.pointAction != null) assigned++;
                if (input.dragAction != null) assigned++;
                if (input.cancelAction != null) assigned++;
                if (input.undoAction != null) assigned++;
                if (input.redoAction != null) assigned++;
                if (input.alternateModifierAction != null) assigned++;
                if (input.rotateAction != null) assigned++;
                if (input.mirrorAction != null) assigned++;

                _footerBoundLabel.text = $"✔ {assigned}/8 references assigned";
                _footerBoundLabel.AddToClassList(assigned == 8 ? "tmbs-pill--ok" : "tmbs-pill--warn");
#else
                _footerBoundLabel.text = "Input System inactive";
                _footerBoundLabel.AddToClassList("tmbs-pill--warn");
#endif
                _footerConflictLabel.text = "Asset-managed bindings";
                _footerConflictLabel.AddToClassList("tmbs-pill--muted");
            }
            else
            {
                _footerBoundLabel.text = input.mode == TmbsInputMode.ExternalProvided
                    ? "External adapter"
                    : "Input disabled";
                _footerBoundLabel.AddToClassList("tmbs-pill--muted");
                _footerConflictLabel.text = "—";
                _footerConflictLabel.AddToClassList("tmbs-pill--muted");
            }
        }
    }
}
