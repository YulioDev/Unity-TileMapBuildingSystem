using System;
using System.Collections.Generic;
using System.IO;
using TMBS.Runtime.Config;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TMBS.Editor.InputConfigurator
{
    public sealed class ControlsView : VisualElement
    {
        private const string ExternalSnippet =
@"public class MyCustomInput : MonoBehaviour
{
    [SerializeField] private BuildeableTilemap buildableTilemap;

    private void Awake()
    {
        // Call before BuildeableTilemap.OnEnable()
        buildableTilemap.SetExternalInputAdapter(new MyBuildInputAdapter());
    }
}";

        private readonly Label _headerLabel;
        private readonly Label _descLabel;
        private readonly VisualElement _legacyBody;
        private readonly VisualElement _inputActionsBody;
        private readonly VisualElement _externalBody;
        private readonly VisualElement _noneBody;

        // Legacy UI
        private readonly Dictionary<TmbsInputActionId, ActionRowElement> _actionRows =
            new Dictionary<TmbsInputActionId, ActionRowElement>();
        private readonly KeyboardMapElement _legacyKeyboard;
        private readonly MouseMapElement _legacyMouse;

        // Input System UI
#if ENABLE_INPUT_SYSTEM
        private VisualElement _inputSystemRefsCol;
        private KeyboardMapElement _isPreviewKeyboard;
        private MouseMapElement _isPreviewMouse;
        private PopupField<string> _genPresetDropdown;
        private Button _pingAssetBtn;
        private ObjectField _pointField;
        private ObjectField _dragField;
        private ObjectField _cancelField;
        private ObjectField _undoField;
        private ObjectField _redoField;
        private ObjectField _altField;
        private ObjectField _rotateField;
        private ObjectField _mirrorField;
#endif

        // Advanced validation toggles
        private readonly Toggle _strictToggle;
        private readonly Toggle _reqPointToggle;
        private readonly Toggle _reqConfirmToggle;
        private readonly Toggle _reqCancelToggle;
        private readonly Toggle _reqDragToggle;
        private readonly Toggle _reqAltToggle;
        private readonly Toggle _allowUndoToggle;
        private readonly Toggle _allowRedoToggle;

        private TmbsRootConfig _config;
        private TmbsInputActionId? _assigningAction;
        private int _assigningSlot = -1;
        private TmbsModifiers _heldModsOnPointer;

        public event Action<string, Action<TmbsRootConfig>> ConfigMutationRequested;
        public event Action<TmbsInputMode> QuickSwitchModeRequested;

        public ControlsView()
        {
            _headerLabel = new Label("2. Configure Controls");
            _headerLabel.AddToClassList("tmbs-section-header");
            Add(_headerLabel);

            _descLabel = new Label();
            _descLabel.AddToClassList("tmbs-section-desc");
            Add(_descLabel);

            // Track modifier keys held when clicking the visual keyboard/mouse
            RegisterCallback<PointerDownEvent>(evt =>
            {
                _heldModsOnPointer = ExtractModifiers(evt.ctrlKey, evt.shiftKey, evt.altKey, evt.commandKey);
            }, TrickleDown.TrickleDown);

            // Capture physical key press during assignment mode
            RegisterCallback<KeyDownEvent>(OnAssignmentKeyDown);
            focusable = true;

            // --- Legacy Body ---
            _legacyBody = new VisualElement();
            Add(_legacyBody);

            var presetBar = new VisualElement();
            presetBar.AddToClassList("tmbs-preset-bar");
            _legacyBody.Add(presetBar);

            var presetsLbl = new Label("Presets:");
            presetsLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            presetsLbl.style.marginRight = 8;
            presetBar.Add(presetsLbl);

            for (int i = 0; i < TmbsInputPresets.All.Count; i++)
            {
                var preset = TmbsInputPresets.All[i];
                var btn = new Button(() => ApplyPreset(preset))
                {
                    text = preset.DisplayName,
                    tooltip = preset.Subtitle
                };
                btn.AddToClassList("tmbs-preset-btn");
                presetBar.Add(btn);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            presetBar.Add(spacer);

            var resetBtn = new Button(ResetLegacyDefaults) { text = "Reset Defaults" };
            resetBtn.AddToClassList("tmbs-preset-btn");
            presetBar.Add(resetBtn);

            var legacySplit = new VisualElement();
            legacySplit.AddToClassList("tmbs-controls-split");
            _legacyBody.Add(legacySplit);

            var actionsCol = new VisualElement();
            actionsCol.AddToClassList("tmbs-actions-col");
            legacySplit.Add(actionsCol);

            for (int i = 0; i < TmbsLegacyBindings.AllActionIds.Length; i++)
            {
                var actionId = TmbsLegacyBindings.AllActionIds[i];
                var row = new ActionRowElement(actionId);
                row.AssignRequested += OnAssignRequested;
                row.ClearRequested += OnClearRequested;
                row.ModifiersChanged += OnModifiersChanged;
                _actionRows[actionId] = row;
                actionsCol.Add(row);
            }

            var mapCol = new VisualElement();
            mapCol.AddToClassList("tmbs-map-col");
            legacySplit.Add(mapCol);

            var devicesRow = new VisualElement();
            devicesRow.AddToClassList("tmbs-devices-row");
            mapCol.Add(devicesRow);

            _legacyKeyboard = new KeyboardMapElement();
            _legacyKeyboard.KeyClicked += OnVisualKeyClicked;
            _legacyKeyboard.CancelAssignRequested += CancelAssignment;
            devicesRow.Add(_legacyKeyboard);

            _legacyMouse = new MouseMapElement();
            _legacyMouse.MouseButtonClicked += OnVisualMouseClicked;
            devicesRow.Add(_legacyMouse);

            // --- InputActions Body ---
            _inputActionsBody = new VisualElement();
            Add(_inputActionsBody);
            BuildInputActionsBody();

            // --- External Body ---
            _externalBody = new VisualElement();
            Add(_externalBody);
            BuildExternalBody();

            // --- None Body ---
            _noneBody = new VisualElement();
            Add(_noneBody);
            BuildNoneBody();

            // --- Advanced Capability Foldout ---
            var advFoldout = new Foldout
            {
                text = "Advanced: Capability Validation & Undo/Redo Flags",
                value = false
            };
            advFoldout.AddToClassList("tmbs-card-panel");
            Add(advFoldout);

            _strictToggle = AddCapToggle(advFoldout, "Strict Input Validation", "Block building if adapter lacks required capabilities.", (cfg, v) => cfg.input.strictInputValidation = v);
            _reqPointToggle = AddCapToggle(advFoldout, "Require Point", null, (cfg, v) => cfg.input.requirePoint = v);
            _reqConfirmToggle = AddCapToggle(advFoldout, "Require Confirm", null, (cfg, v) => cfg.input.requireConfirm = v);
            _reqCancelToggle = AddCapToggle(advFoldout, "Require Cancel", null, (cfg, v) => cfg.input.requireCancel = v);
            _reqDragToggle = AddCapToggle(advFoldout, "Require Drag", null, (cfg, v) => cfg.input.requireDrag = v);
            _reqAltToggle = AddCapToggle(advFoldout, "Require Alternate Modifier (Erase)", null, (cfg, v) => cfg.input.requireAlternateModifier = v);
            _allowUndoToggle = AddCapToggle(advFoldout, "Allow Undo Input", null, (cfg, v) => cfg.input.allowUndoInput = v);
            _allowRedoToggle = AddCapToggle(advFoldout, "Allow Redo Input", null, (cfg, v) => cfg.input.allowRedoInput = v);
        }

        private Toggle AddCapToggle(VisualElement parent, string label, string tip, Action<TmbsRootConfig, bool> setter)
        {
            var toggle = new Toggle(label) { tooltip = tip };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (_config == null || _config.input == null)
                    return;
                ConfigMutationRequested?.Invoke($"Toggle {label}", cfg => setter(cfg, evt.newValue));
            });
            parent.Add(toggle);
            return toggle;
        }

        private void BuildInputActionsBody()
        {
#if ENABLE_INPUT_SYSTEM
            var topBar = new VisualElement();
            topBar.AddToClassList("tmbs-preset-bar");
            _inputActionsBody.Add(topBar);

            var presetNames = new List<string>();
            for (int i = 0; i < TmbsInputPresets.All.Count; i++)
                presetNames.Add(TmbsInputPresets.All[i].DisplayName);

            _genPresetDropdown = new PopupField<string>("Template:", presetNames, 0);
            _genPresetDropdown.style.minWidth = 200;
            _genPresetDropdown.style.marginRight = 8;
            topBar.Add(_genPresetDropdown);

            var generateBtn = new Button(OnGenerateActionsClicked)
            {
                text = "Generate & Assign TMBS Input Actions"
            };
            generateBtn.AddToClassList("tmbs-primary-btn");
            generateBtn.style.marginRight = 6;
            topBar.Add(generateBtn);

            _pingAssetBtn = new Button(OnPingInputAssetClicked) { text = "Ping Asset" };
            topBar.Add(_pingAssetBtn);

            var split = new VisualElement();
            split.AddToClassList("tmbs-controls-split");
            _inputActionsBody.Add(split);

            _inputSystemRefsCol = new VisualElement();
            _inputSystemRefsCol.AddToClassList("tmbs-actions-col");
            _inputSystemRefsCol.AddToClassList("tmbs-card-panel");
            split.Add(_inputSystemRefsCol);

            var refsTitle = new Label("InputActionReferences");
            refsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            refsTitle.style.marginBottom = 6;
            _inputSystemRefsCol.Add(refsTitle);

            _pointField = AddActionRefField("Point (Vector2)", (c, r) => c.input.pointAction = r);
            _dragField = AddActionRefField("Place / Drag (Button)", (c, r) => c.input.dragAction = r);
            _cancelField = AddActionRefField("Cancel (Button)", (c, r) => c.input.cancelAction = r);
            _undoField = AddActionRefField("Undo (Button)", (c, r) => c.input.undoAction = r);
            _redoField = AddActionRefField("Redo (Button)", (c, r) => c.input.redoAction = r);
            _altField = AddActionRefField("Alternate / Erase (Button)", (c, r) => c.input.alternateModifierAction = r);
            _rotateField = AddActionRefField("Rotate (Button)", (c, r) => c.input.rotateAction = r);
            _mirrorField = AddActionRefField("Mirror (Button)", (c, r) => c.input.mirrorAction = r);

            var mapCol = new VisualElement();
            mapCol.AddToClassList("tmbs-map-col");
            split.Add(mapCol);

            var devicesRow = new VisualElement();
            devicesRow.AddToClassList("tmbs-devices-row");
            mapCol.Add(devicesRow);

            _isPreviewKeyboard = new KeyboardMapElement();
            _isPreviewKeyboard.SetReadOnly(true, "Read-only visual preview parsed from your assigned InputActionReferences.");
            devicesRow.Add(_isPreviewKeyboard);

            _isPreviewMouse = new MouseMapElement();
            _isPreviewMouse.SetReadOnly(true);
            devicesRow.Add(_isPreviewMouse);
#else
            var warnCard = new VisualElement();
            warnCard.AddToClassList("tmbs-card-panel");
            _inputActionsBody.Add(warnCard);

            var title = new Label("Input System is currently inactive");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            warnCard.Add(title);

            var msg = new Label(
                "To edit InputActionReferences and auto-generate TMBS_Actions.inputactions, ensure 'com.unity.inputsystem' " +
                "is installed and set Project Settings ▸ Player ▸ Active Input Handling to 'Input System Package (New)' or 'Both'.");
            msg.style.whiteSpace = WhiteSpace.Normal;
            msg.style.marginBottom = 8;
            warnCard.Add(msg);

            var btn = new Button(() => SettingsService.OpenProjectSettings("Project/Player"))
            {
                text = "Open Player Settings"
            };
            btn.AddToClassList("tmbs-primary-btn");
            btn.style.alignSelf = Align.FlexStart;
            warnCard.Add(btn);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private ObjectField AddActionRefField(string label, Action<TmbsRootConfig, InputActionReference> setter)
        {
            var field = new ObjectField(label)
            {
                objectType = typeof(InputActionReference),
                allowSceneObjects = false
            };
            field.style.marginBottom = 4;
            field.RegisterValueChangedCallback(evt =>
            {
                if (_config == null || _config.input == null)
                    return;

                var refAsset = evt.newValue as InputActionReference;
                ConfigMutationRequested?.Invoke($"Assign {label}", cfg => setter(cfg, refAsset));
            });
            _inputSystemRefsCol.Add(field);
            return field;
        }
#endif

        private void BuildExternalBody()
        {
            var card = new VisualElement();
            card.AddToClassList("tmbs-card-panel");
            _externalBody.Add(card);

            var title = new Label("External / Custom Input Adapter Setup");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13;
            title.style.marginBottom = 4;
            card.Add(title);

            var body = new Label(
                "Implement TMBS.Core.Input.IBuildInputAdapter (and optionally IUpdatableInput if it needs per-frame Tick) " +
                "and supply it to BuildeableTilemap before OnEnable() runs:");
            body.style.whiteSpace = WhiteSpace.Normal;
            card.Add(body);

            var code = new Label(ExternalSnippet);
            code.AddToClassList("tmbs-code-box");
            card.Add(code);

            var copyBtn = new Button(() =>
            {
                GUIUtility.systemCopyBuffer = ExternalSnippet;
                Debug.Log("TMBS: External adapter snippet copied to clipboard.");
            })
            {
                text = "Copy C# Snippet"
            };
            copyBtn.style.alignSelf = Align.FlexStart;
            card.Add(copyBtn);
        }

        private void BuildNoneBody()
        {
            var card = new VisualElement();
            card.AddToClassList("tmbs-card-panel");
            _noneBody.Add(card);

            var title = new Label("Input Processing Disabled (Mode = None)");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            card.Add(title);

            var msg = new Label(
                "No pointer or keyboard input adapter will be created. Building and erasing can still be triggered via " +
                "BuildeableTilemap public APIs, or you can switch back to Legacy or Input System below.");
            msg.style.whiteSpace = WhiteSpace.Normal;
            msg.style.marginBottom = 8;
            card.Add(msg);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            card.Add(row);

            var toLegacy = new Button(() => QuickSwitchModeRequested?.Invoke(TmbsInputMode.Legacy))
            {
                text = "Switch to Legacy"
            };
            toLegacy.style.marginRight = 6;
            row.Add(toLegacy);

            var toInputSys = new Button(() => QuickSwitchModeRequested?.Invoke(TmbsInputMode.InputActions))
            {
                text = "Switch to Input System"
            };
            row.Add(toInputSys);
        }

        public void Refresh(TmbsRootConfig config)
        {
            _config = config;

            if (config == null || config.input == null)
            {
                _legacyBody.style.display = DisplayStyle.None;
                _inputActionsBody.style.display = DisplayStyle.None;
                _externalBody.style.display = DisplayStyle.None;
                _noneBody.style.display = DisplayStyle.None;
                _descLabel.text = "Select or create a TmbsRootConfig asset above to edit controls.";
                return;
            }

            var mode = config.input.mode;
            if (mode != TmbsInputMode.Legacy)
                CancelAssignment();

            _legacyBody.style.display = mode == TmbsInputMode.Legacy ? DisplayStyle.Flex : DisplayStyle.None;
            _inputActionsBody.style.display = mode == TmbsInputMode.InputActions ? DisplayStyle.Flex : DisplayStyle.None;
            _externalBody.style.display = mode == TmbsInputMode.ExternalProvided ? DisplayStyle.Flex : DisplayStyle.None;
            _noneBody.style.display = mode == TmbsInputMode.None ? DisplayStyle.Flex : DisplayStyle.None;

            switch (mode)
            {
                case TmbsInputMode.Legacy:
                    _descLabel.text = "Click an action chip on the left, then click a key on the keyboard or a mouse button on the right.";
                    RefreshLegacyBindings(config.input.GetLegacyBindings());
                    break;
                case TmbsInputMode.InputActions:
                    _descLabel.text = "Assign InputActionReferences or auto-generate a ready-to-use .inputactions asset.";
                    RefreshInputActionsView(config.input);
                    break;
                case TmbsInputMode.ExternalProvided:
                    _descLabel.text = "Input is supplied externally through your own C# adapter.";
                    break;
                case TmbsInputMode.None:
                    _descLabel.text = "Automatic input polling is turned off.";
                    break;
            }

            _strictToggle.SetValueWithoutNotify(config.input.strictInputValidation);
            _reqPointToggle.SetValueWithoutNotify(config.input.requirePoint);
            _reqConfirmToggle.SetValueWithoutNotify(config.input.requireConfirm);
            _reqCancelToggle.SetValueWithoutNotify(config.input.requireCancel);
            _reqDragToggle.SetValueWithoutNotify(config.input.requireDrag);
            _reqAltToggle.SetValueWithoutNotify(config.input.requireAlternateModifier);
            _allowUndoToggle.SetValueWithoutNotify(config.input.allowUndoInput);
            _allowRedoToggle.SetValueWithoutNotify(config.input.allowRedoInput);
        }

        private void RefreshLegacyBindings(TmbsLegacyBindings bindings)
        {
            foreach (var kvp in _actionRows)
            {
                int slot = (_assigningAction.HasValue && _assigningAction.Value == kvp.Key) ? _assigningSlot : -1;
                kvp.Value.Bind(bindings, slot);
            }

            _legacyKeyboard.SetAssigning(_assigningAction, _assigningSlot);
            _legacyKeyboard.UpdateBindings(bindings);
            _legacyMouse.UpdateBindings(bindings);
        }

        private void RefreshInputActionsView(TmbsInputConfig inputConfig)
        {
#if ENABLE_INPUT_SYSTEM
            _pointField.SetValueWithoutNotify(inputConfig.pointAction);
            _dragField.SetValueWithoutNotify(inputConfig.dragAction);
            _cancelField.SetValueWithoutNotify(inputConfig.cancelAction);
            _undoField.SetValueWithoutNotify(inputConfig.undoAction);
            _redoField.SetValueWithoutNotify(inputConfig.redoAction);
            _altField.SetValueWithoutNotify(inputConfig.alternateModifierAction);
            _rotateField.SetValueWithoutNotify(inputConfig.rotateAction);
            _mirrorField.SetValueWithoutNotify(inputConfig.mirrorAction);

            bool hasAnyAsset = FindFirstAssignedAsset(inputConfig) != null;
            _pingAssetBtn.SetEnabled(hasAnyAsset);

            var previewBindings = InputSystemBindingParser.ParseFromConfig(inputConfig);
            _isPreviewKeyboard.UpdateBindings(previewBindings);
            _isPreviewMouse.UpdateBindings(previewBindings);
#endif
        }

        public void CancelAssignment()
        {
            if (!_assigningAction.HasValue)
                return;

            _assigningAction = null;
            _assigningSlot = -1;
            if (_config != null && _config.input != null)
                RefreshLegacyBindings(_config.input.GetLegacyBindings());
        }

        private void OnAssignRequested(TmbsInputActionId actionId, int slot)
        {
            if (_assigningAction.HasValue && _assigningAction.Value == actionId && _assigningSlot == slot)
            {
                CancelAssignment();
                return;
            }

            _assigningAction = actionId;
            _assigningSlot = slot;
            Focus();
            if (_config != null && _config.input != null)
                RefreshLegacyBindings(_config.input.GetLegacyBindings());
        }

        private void OnClearRequested(TmbsInputActionId actionId, int slot)
        {
            if (_assigningAction == actionId && _assigningSlot == slot)
                CancelAssignment();

            ConfigMutationRequested?.Invoke($"Clear {actionId} binding", cfg =>
            {
                cfg.input.GetLegacyBindings().SetSlot(actionId, slot, TmbsKeyBinding.None);
            });
        }

        private void OnModifiersChanged(TmbsInputActionId actionId, int slot, TmbsModifiers newModifiers)
        {
            ConfigMutationRequested?.Invoke($"Toggle {actionId} modifiers", cfg =>
            {
                var legacy = cfg.input.GetLegacyBindings();
                var kb = legacy.GetSlot(actionId, slot);
                kb.modifiers = newModifiers;
                legacy.SetSlot(actionId, slot, kb);
            });
        }

        private void OnVisualKeyClicked(KeyCode key)
        {
            if (!_assigningAction.HasValue || _assigningSlot < 0)
                return;

            var targetAction = _assigningAction.Value;
            int targetSlot = _assigningSlot;

            var candidate = TmbsKeyBinding.Key(key, _heldModsOnPointer);
            var selfMod = TMBS.Unity.Input.LegacyBindingEvaluator.GetSelfModifier(in candidate);
            candidate.modifiers &= ~selfMod;

            _assigningAction = null;
            _assigningSlot = -1;

            ConfigMutationRequested?.Invoke($"Bind {targetAction} to {candidate.ToDisplayString()}", cfg =>
            {
                cfg.input.GetLegacyBindings().SetSlot(targetAction, targetSlot, candidate);
            });
        }

        private void OnVisualMouseClicked(int mouseButton)
        {
            if (!_assigningAction.HasValue || _assigningSlot < 0)
                return;

            var targetAction = _assigningAction.Value;
            int targetSlot = _assigningSlot;
            var candidate = TmbsKeyBinding.Mouse(mouseButton, _heldModsOnPointer);

            _assigningAction = null;
            _assigningSlot = -1;

            ConfigMutationRequested?.Invoke($"Bind {targetAction} to {candidate.ToDisplayString()}", cfg =>
            {
                cfg.input.GetLegacyBindings().SetSlot(targetAction, targetSlot, candidate);
            });
        }

        private void OnAssignmentKeyDown(KeyDownEvent evt)
        {
            if (!_assigningAction.HasValue || _assigningSlot < 0)
                return;

            if (evt.keyCode == KeyCode.None || IsPureModifierKey(evt.keyCode))
                return;

            var mods = ExtractModifiers(evt.ctrlKey, evt.shiftKey, evt.altKey, evt.commandKey);
            var candidate = TmbsKeyBinding.Key(evt.keyCode, mods);
            var targetAction = _assigningAction.Value;
            int targetSlot = _assigningSlot;

            _assigningAction = null;
            _assigningSlot = -1;
            evt.StopPropagation();

            ConfigMutationRequested?.Invoke($"Bind {targetAction} to {candidate.ToDisplayString()}", cfg =>
            {
                cfg.input.GetLegacyBindings().SetSlot(targetAction, targetSlot, candidate);
            });
        }

        private void ApplyPreset(TmbsInputPreset preset)
        {
            CancelAssignment();
            ConfigMutationRequested?.Invoke($"Apply {preset.DisplayName} preset", cfg =>
            {
                preset.ApplyTo(cfg.input.GetLegacyBindings());
            });
        }

        private void ResetLegacyDefaults()
        {
            CancelAssignment();
            ConfigMutationRequested?.Invoke("Reset Legacy bindings to defaults", cfg =>
            {
                cfg.input.GetLegacyBindings().CopyFrom(TmbsLegacyBindings.CreateDefault());
            });
        }

#if ENABLE_INPUT_SYSTEM
        private void OnGenerateActionsClicked()
        {
            if (_config == null)
                return;

            if (!Directory.Exists(TmbsInputActionsGenerator.DefaultFolder))
            {
                Directory.CreateDirectory(TmbsInputActionsGenerator.DefaultFolder);
                AssetDatabase.Refresh();
            }

            string savePath = EditorUtility.SaveFilePanelInProject(
                "Generate TMBS Input Actions",
                "TMBS_Actions",
                "inputactions",
                "Choose where to save the generated Input Actions asset.",
                TmbsInputActionsGenerator.DefaultFolder);

            if (string.IsNullOrEmpty(savePath))
                return;

            int presetIdx = Mathf.Clamp(_genPresetDropdown.index, 0, TmbsInputPresets.All.Count - 1);
            var template = TmbsInputPresets.All[presetIdx].CreateBindings();

            var generated = TmbsInputActionsGenerator.GenerateAndAssign(_config, savePath, template);
            if (generated != null)
            {
                EditorGUIUtility.PingObject(generated);
                Refresh(_config);
            }
        }

        private void OnPingInputAssetClicked()
        {
            if (_config == null || _config.input == null)
                return;

            var asset = FindFirstAssignedAsset(_config.input);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        private static InputActionAsset FindFirstAssignedAsset(TmbsInputConfig inputConfig)
        {
            InputActionReference[] refs =
            {
                inputConfig.dragAction, inputConfig.pointAction, inputConfig.cancelAction,
                inputConfig.undoAction, inputConfig.redoAction, inputConfig.alternateModifierAction,
                inputConfig.rotateAction, inputConfig.mirrorAction
            };

            for (int i = 0; i < refs.Length; i++)
            {
                if (refs[i] != null && refs[i].asset != null)
                    return refs[i].asset;
            }
            return null;
        }
#endif

        private static TmbsModifiers ExtractModifiers(bool ctrl, bool shift, bool alt, bool cmd)
        {
            var mods = TmbsModifiers.None;
            if (ctrl) mods |= TmbsModifiers.Ctrl;
            if (shift) mods |= TmbsModifiers.Shift;
            if (alt) mods |= TmbsModifiers.Alt;
            if (cmd) mods |= TmbsModifiers.Command;
            return mods;
        }

        private static bool IsPureModifierKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                    return true;
                default:
                    return false;
            }
        }
    }
}
