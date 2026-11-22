using System;
using System.Collections.Generic;
using TMBS.Core.Input;
using TMBS.Core.Intents;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using TMBS.Unity.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TMBS.Editor.InputConfigurator
{
    public sealed class LiveTesterView : VisualElement
    {
        private struct LogEntry
        {
            public BuildIntent Intent;
            public Vector3Int Cell;
            public int Frame;
            public bool HasCell;
        }

        private readonly VisualElement _editModeCard;
        private readonly VisualElement _playModeContainer;
        private readonly Label _instanceNameLabel;
        private readonly Label _adapterNameLabel;
        private readonly VisualElement _capabilitiesRow;
        private readonly Label _cellValueLabel;
        private readonly Label _worldValueLabel;
        private readonly Label _stateValueLabel;
        private readonly Label _totalCountLabel;
        private readonly VisualElement _countersRow;
        private readonly ScrollView _logScroll;
        private readonly Toggle _logPointMoveToggle;
        private readonly KeyboardMapElement _compactKeyboard;
        private readonly MouseMapElement _compactMouse;

        private readonly Dictionary<BuildIntentType, int> _intentCounts = new Dictionary<BuildIntentType, int>();
        private readonly Dictionary<BuildIntentType, Label> _counterPills = new Dictionary<BuildIntentType, Label>();
        private readonly List<LogEntry> _logEntries = new List<LogEntry>(64);
        private readonly List<BuildeableTilemap> _sceneInstances = new List<BuildeableTilemap>();

        private BuildeableTilemap _observedTarget;
        private int _selectedInstanceIndex;
        private int _totalIntents;
        private bool _isDraggingState;
        private bool _isEraseState;
        private Vector3Int _lastCell;
        private Vector3 _lastWorldPoint;
        private bool _hasLastPoint;
        private bool _logDirty;

        public LiveTesterView()
        {
            var header = new Label("3. Live Input Tester");
            header.AddToClassList("tmbs-section-header");
            Add(header);

            var desc = new Label("Inspect the active adapter, capabilities, hovered tile cell, and BuildIntent stream in Play Mode.");
            desc.AddToClassList("tmbs-section-desc");
            Add(desc);

            // Edit Mode prompt
            _editModeCard = new VisualElement();
            _editModeCard.AddToClassList("tmbs-card-panel");
            _editModeCard.style.alignItems = Align.Center;
            _editModeCard.style.paddingTop = 24;
            _editModeCard.style.paddingBottom = 24;
            Add(_editModeCard);

            var editMsg = new Label("Enter Play Mode to observe live BuildIntents and test your bindings in real time.");
            editMsg.style.fontSize = 12;
            editMsg.style.marginBottom = 10;
            _editModeCard.Add(editMsg);

            var enterPlayBtn = new Button(() => EditorApplication.isPlaying = true)
            {
                text = "Enter Play Mode"
            };
            enterPlayBtn.AddToClassList("tmbs-primary-btn");
            _editModeCard.Add(enterPlayBtn);

            // Play Mode view
            _playModeContainer = new VisualElement();
            Add(_playModeContainer);

            // Top target bar
            var targetBar = new VisualElement();
            targetBar.AddToClassList("tmbs-preset-bar");
            targetBar.style.justifyContent = Justify.SpaceBetween;
            _playModeContainer.Add(targetBar);

            var leftTarget = new VisualElement();
            leftTarget.style.flexDirection = FlexDirection.Row;
            leftTarget.style.alignItems = Align.Center;
            targetBar.Add(leftTarget);

            _instanceNameLabel = new Label("Target: None");
            _instanceNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _instanceNameLabel.style.marginRight = 8;
            leftTarget.Add(_instanceNameLabel);

            var nextBtn = new Button(CycleTargetInstance) { text = "Switch Target" };
            nextBtn.style.marginRight = 6;
            leftTarget.Add(nextBtn);

            var refreshBtn = new Button(RefreshTargetAndAttach) { text = "Refresh" };
            leftTarget.Add(refreshBtn);

            _adapterNameLabel = new Label("Adapter: —");
            _adapterNameLabel.AddToClassList("tmbs-pill");
            _adapterNameLabel.AddToClassList("tmbs-pill--muted");
            targetBar.Add(_adapterNameLabel);

            // Capabilities row
            var capCard = new VisualElement();
            capCard.AddToClassList("tmbs-card-panel");
            _playModeContainer.Add(capCard);

            var capTitle = new Label("Active Input Capabilities");
            capTitle.style.fontSize = 11;
            capTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            capTitle.style.marginBottom = 5;
            capCard.Add(capTitle);

            _capabilitiesRow = new VisualElement();
            _capabilitiesRow.style.flexDirection = FlexDirection.Row;
            _capabilitiesRow.style.flexWrap = Wrap.Wrap;
            capCard.Add(_capabilitiesRow);

            // Telemetry cards
            var telemetryGrid = new VisualElement();
            telemetryGrid.AddToClassList("tmbs-telemetry-grid");
            _playModeContainer.Add(telemetryGrid);

            _cellValueLabel = AddTelemetryCard(telemetryGrid, "Hovered Cell");
            _worldValueLabel = AddTelemetryCard(telemetryGrid, "World Point");
            _stateValueLabel = AddTelemetryCard(telemetryGrid, "Pointer / Erase State");
            _totalCountLabel = AddTelemetryCard(telemetryGrid, "Total Intents");

            // Counters row
            _countersRow = new VisualElement();
            _countersRow.AddToClassList("tmbs-card-panel");
            _countersRow.style.flexDirection = FlexDirection.Row;
            _countersRow.style.flexWrap = Wrap.Wrap;
            _playModeContainer.Add(_countersRow);

            foreach (BuildIntentType type in Enum.GetValues(typeof(BuildIntentType)))
            {
                var pill = new Label($"{type}: 0");
                pill.AddToClassList("tmbs-tag");
                pill.style.borderLeftWidth = 3;
                pill.style.borderLeftColor = TmbsActionCatalog.GetIntentColor(type);
                _countersRow.Add(pill);
                _counterPills[type] = pill;
                _intentCounts[type] = 0;
            }

            // Compact live keyboard + mouse monitor
            var devicesRow = new VisualElement();
            devicesRow.AddToClassList("tmbs-devices-row");
            _playModeContainer.Add(devicesRow);

            _compactKeyboard = new KeyboardMapElement(compact: true);
            _compactKeyboard.SetReadOnly(true, "Live key monitor");
            devicesRow.Add(_compactKeyboard);

            _compactMouse = new MouseMapElement(compact: true);
            _compactMouse.SetReadOnly(true);
            devicesRow.Add(_compactMouse);

            // Log header & scroll
            var logHeader = new VisualElement();
            logHeader.style.flexDirection = FlexDirection.Row;
            logHeader.style.alignItems = Align.Center;
            logHeader.style.justifyContent = Justify.SpaceBetween;
            logHeader.style.marginTop = 4;
            logHeader.style.marginBottom = 4;
            _playModeContainer.Add(logHeader);

            var logTitle = new Label("Recent BuildIntents (latest 50)");
            logTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            logHeader.Add(logTitle);

            var logControls = new VisualElement();
            logControls.style.flexDirection = FlexDirection.Row;
            logControls.style.alignItems = Align.Center;
            logHeader.Add(logControls);

            _logPointMoveToggle = new Toggle("Log PointMove") { value = false };
            _logPointMoveToggle.style.marginRight = 8;
            logControls.Add(_logPointMoveToggle);

            var clearBtn = new Button(ClearLog) { text = "Clear Log" };
            logControls.Add(clearBtn);

            _logScroll = new ScrollView();
            _logScroll.AddToClassList("tmbs-log-scroll");
            _playModeContainer.Add(_logScroll);

            RefreshPlayModeState(null);
        }

        private static Label AddTelemetryCard(VisualElement parent, string labelText)
        {
            var card = new VisualElement();
            card.AddToClassList("tmbs-telemetry-card");
            parent.Add(card);

            var title = new Label(labelText);
            title.AddToClassList("tmbs-telemetry-label");
            card.Add(title);

            var value = new Label("—");
            value.AddToClassList("tmbs-telemetry-value");
            card.Add(value);
            return value;
        }

        public void RefreshPlayModeState(TmbsRootConfig config)
        {
            bool playing = EditorApplication.isPlaying;
            _editModeCard.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;
            _playModeContainer.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;

            var bindings = config != null && config.input != null
                ? config.input.GetLegacyBindings()
                : TmbsLegacyBindings.CreateDefault();
            _compactKeyboard.UpdateBindings(bindings);
            _compactMouse.UpdateBindings(bindings);

            if (playing)
            {
                RefreshTargetAndAttach();
            }
            else
            {
                Detach();
            }
        }

        public void Detach()
        {
            if (_observedTarget != null)
            {
                _observedTarget.IntentObserved -= OnIntentObserved;
                _observedTarget = null;
            }
        }

        private void CycleTargetInstance()
        {
            if (_sceneInstances.Count > 1)
            {
                _selectedInstanceIndex = (_selectedInstanceIndex + 1) % _sceneInstances.Count;
            }
            RefreshTargetAndAttach();
        }

        private void RefreshTargetAndAttach()
        {
            _sceneInstances.Clear();
#if UNITY_6000_3_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<BuildeableTilemap>();
#elif UNITY_2023_1_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<BuildeableTilemap>(FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType<BuildeableTilemap>();
#endif
            if (found != null)
                _sceneInstances.AddRange(found);

            if (_sceneInstances.Count == 0)
            {
                Detach();
                _instanceNameLabel.text = "Target: No BuildeableTilemap found in scene";
                _adapterNameLabel.text = "Adapter: None";
                RefreshCapabilities(default, hasAdapter: false);
                return;
            }

            if (_selectedInstanceIndex >= _sceneInstances.Count)
                _selectedInstanceIndex = 0;

            var next = _sceneInstances[_selectedInstanceIndex];
            if (_observedTarget != next)
            {
                Detach();
                _observedTarget = next;
                _observedTarget.IntentObserved += OnIntentObserved;
            }

            _instanceNameLabel.text = $"Target: {_observedTarget.name} ({_selectedInstanceIndex + 1}/{_sceneInstances.Count})";

            var adapter = _observedTarget.ActiveInputAdapter;
            _adapterNameLabel.text = adapter != null ? $"Adapter: {adapter.GetType().Name}" : "Adapter: None";
            RefreshCapabilities(adapter != null ? adapter.Capabilities : default, adapter != null);

            if (_observedTarget.RootConfig != null && _observedTarget.RootConfig.input != null)
            {
                var bindings = _observedTarget.RootConfig.input.GetLegacyBindings();
                _compactKeyboard.UpdateBindings(bindings);
                _compactMouse.UpdateBindings(bindings);
            }
        }

        private void RefreshCapabilities(InputCapabilities caps, bool hasAdapter)
        {
            _capabilitiesRow.Clear();
            AddCapabilityPill("Point", hasAdapter && caps.HasPoint);
            AddCapabilityPill("Confirm", hasAdapter && caps.HasConfirm);
            AddCapabilityPill("Cancel", hasAdapter && caps.HasCancel);
            AddCapabilityPill("Drag", hasAdapter && caps.HasDrag);
            AddCapabilityPill("Undo", hasAdapter && caps.HasUndo);
            AddCapabilityPill("Redo", hasAdapter && caps.HasRedo);
            AddCapabilityPill("Erase Mod", hasAdapter && caps.HasAlternateModifier);
        }

        private void AddCapabilityPill(string label, bool supported)
        {
            var pill = new Label(supported ? $"✔ {label}" : $"✖ {label}");
            pill.AddToClassList("tmbs-pill");
            pill.AddToClassList(supported ? "tmbs-pill--ok" : "tmbs-pill--bad");
            pill.style.marginRight = 5;
            pill.style.marginBottom = 3;
            _capabilitiesRow.Add(pill);
        }

        private void OnIntentObserved(BuildIntent intent)
        {
            _totalIntents++;
            if (_intentCounts.TryGetValue(intent.Type, out int count))
                _intentCounts[intent.Type] = count + 1;
            else
                _intentCounts[intent.Type] = 1;

            _lastWorldPoint = intent.WorldPoint;
            bool hasCell = _observedTarget != null && _observedTarget.TargetTilemap != null;
            if (hasCell)
                _lastCell = _observedTarget.TargetTilemap.WorldToCell(intent.WorldPoint);
            _hasLastPoint = true;

            if (intent.Type == BuildIntentType.DragStart)
                _isDraggingState = true;
            else if (intent.Type == BuildIntentType.DragEnd || intent.Type == BuildIntentType.Cancel || intent.Type == BuildIntentType.Confirm)
                _isDraggingState = false;

            _isEraseState = intent.AlternateBehaviour || (_observedTarget != null && _observedTarget.IsEraseMode);

            if (intent.Type == BuildIntentType.PointMove && !_logPointMoveToggle.value)
                return;

            _logEntries.Insert(0, new LogEntry
            {
                Intent = intent,
                Cell = _lastCell,
                HasCell = hasCell,
                Frame = Time.frameCount
            });

            if (_logEntries.Count > 50)
                _logEntries.RemoveAt(_logEntries.Count - 1);

            _logDirty = true;
        }

        public void TickUI()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (_observedTarget == null)
                RefreshTargetAndAttach();

            _cellValueLabel.text = _hasLastPoint
                ? $"({_lastCell.x}, {_lastCell.y}, {_lastCell.z})"
                : "—";

            _worldValueLabel.text = _hasLastPoint
                ? $"({_lastWorldPoint.x:F2}, {_lastWorldPoint.y:F2}, {_lastWorldPoint.z:F2})"
                : "—";

            string stateStr = _isDraggingState ? "Dragging" : "Idle";
            if (_isEraseState)
                stateStr += " · ERASE";
            _stateValueLabel.text = stateStr;
            _totalCountLabel.text = _totalIntents.ToString();

            foreach (var kvp in _counterPills)
            {
                int count = _intentCounts.TryGetValue(kvp.Key, out int c) ? c : 0;
                kvp.Value.text = $"{kvp.Key}: {count}";
            }

            if (UnityLegacyInputSource.Instance.IsAvailable)
            {
                _compactKeyboard.UpdatePressedState(k => UnityLegacyInputSource.Instance.GetKey(k));
                _compactMouse.UpdatePressedState(b => UnityLegacyInputSource.Instance.GetMouseButton(b));
            }
            else
            {
                _compactKeyboard.UpdatePressedState(null);
                _compactMouse.UpdatePressedState(null);
            }

            if (_logDirty)
            {
                _logDirty = false;
                RebuildLogVisuals();
            }
        }

        private void RebuildLogVisuals()
        {
            _logScroll.Clear();
            for (int i = 0; i < _logEntries.Count; i++)
            {
                var entry = _logEntries[i];
                var row = new VisualElement();
                row.AddToClassList("tmbs-log-entry");

                var badge = new Label(entry.Intent.Type.ToString());
                badge.AddToClassList("tmbs-log-badge");
                badge.style.backgroundColor = TmbsActionCatalog.GetIntentColor(entry.Intent.Type);
                row.Add(badge);

                string cellStr = entry.HasCell ? $"Cell ({entry.Cell.x}, {entry.Cell.y})" : $"World ({entry.Intent.WorldPoint.x:F1}, {entry.Intent.WorldPoint.y:F1})";
                string altStr = entry.Intent.AlternateBehaviour ? "  [ALT/ERASE]" : string.Empty;
                var info = new Label($"f{entry.Frame}  ·  {cellStr}{altStr}");
                info.style.fontSize = 11;
                row.Add(info);

                _logScroll.Add(row);
            }
        }

        private void ClearLog()
        {
            _logEntries.Clear();
            _totalIntents = 0;
            var keys = new List<BuildIntentType>(_intentCounts.Keys);
            for (int i = 0; i < keys.Count; i++)
                _intentCounts[keys[i]] = 0;

            _logDirty = true;
            RebuildLogVisuals();
        }
    }
}
