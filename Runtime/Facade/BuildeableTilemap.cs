using System.Collections.Generic;
using TMBS.Core.Catalog;
using TMBS.Core.Events;
using TMBS.Core.Execution;
using TMBS.Core.Focus;
using TMBS.Core.History;
using TMBS.Core.Input;
using TMBS.Core.Intents;
using TMBS.Core.Metadata;
using TMBS.Core.Modes;
using TMBS.Core.Pipeline;
using TMBS.Core.Preview;
using TMBS.Core.Selection;
using TMBS.Core.Validation;
using TMBS.Runtime.Config;
using TMBS.Unity.Preview;
using TMBS.Unity.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Runtime.Facade
{
    public sealed class BuildeableTilemap : MonoBehaviour
    {
        [Header("Configuration Asset")]
        [SerializeField] private TmbsRootConfig rootConfig;
        [SerializeField] private string instanceId = "TMBS_Instance";

        [Header("Scene References")]
        [SerializeField] private MonoBehaviour inputAdapter;
        [SerializeField] private Tilemap targetTilemap;
        [SerializeField] private Tilemap previewTilemap;

        private IBuildInputAdapter _input;
        private IBuildPipeline _pipeline;
        private IEventBus _events;
        private IInputFocusService _focus;
        private IUndoRedoHistory _history;
        private IMetadataStore _metadata;
        private IPreviewRenderer _preview;
        private TileSelectionState _tileSelectionState;
        private ImmediateBuildExecutor _executor;
        private PreviewPolicyEvaluator _previewEvaluator;
        private IBuildMode _activeMode;

        private bool TryValidateSerializedDependencies(out IBuildInputAdapter resolvedInput)
        {
            resolvedInput = null;

            if (rootConfig == null)
            {
                Debug.LogError("TMBS: rootConfig no está asignado.", this);
                return false;
            }

            if (targetTilemap == null)
            {
                Debug.LogError("TMBS: targetTilemap no está asignado.", this);
                return false;
            }

            if (previewTilemap == null)
            {
                Debug.LogError("TMBS: previewTilemap no está asignado.", this);
                return false;
            }

            return TryResolveInputAdapter(out resolvedInput);
        }

        private bool TryResolveInputAdapter(out IBuildInputAdapter resolvedInput)
        {
            resolvedInput = null;

            var inputConfig = rootConfig.input;

            if (inputConfig == null || inputConfig.mode == TmbsInputMode.None)
            {
                return true;
            }

            switch (inputConfig.mode)
            {
                case TmbsInputMode.SceneProvided:
                case TmbsInputMode.Legacy:
                case TmbsInputMode.ModernInputSystem:
                    resolvedInput = inputAdapter as IBuildInputAdapter;
                    break;

                case TmbsInputMode.DebugMouse:
                    resolvedInput = ResolveOrCreateDebugMouseAdapter(inputConfig);
                    break;

                default:
                    Debug.LogError($"TMBS: input mode no soportado: {inputConfig.mode}.", this);
                    return false;
            }

            if (resolvedInput == null)
            {
                Debug.LogError($"TMBS: no se pudo resolver input adapter para modo {inputConfig.mode}.", this);
                return false;
            }

            return ValidateInputCapabilities(resolvedInput.Capabilities, inputConfig);
        }

        private IBuildInputAdapter ResolveOrCreateDebugMouseAdapter(TmbsInputConfig config)
        {
            var existing = GetComponent<TMBS.Unity.Input.LegacyMouseInputAdapter>();

            if (existing != null)
                return existing;

            if (!config.autoCreateDebugInputAdapter)
                return inputAdapter as IBuildInputAdapter;

            return gameObject.AddComponent<TMBS.Unity.Input.LegacyMouseInputAdapter>();
        }

        private bool ValidateInputCapabilities(InputCapabilities capabilities, TmbsInputConfig config)
        {
            bool valid = true;

            valid &= ValidateCapability(!config.requirePoint || capabilities.HasPoint, "Point", config);
            valid &= ValidateCapability(!config.requireConfirm || capabilities.HasConfirm, "Confirm", config);
            valid &= ValidateCapability(!config.requireCancel || capabilities.HasCancel, "Cancel", config);
            valid &= ValidateCapability(!config.requireDrag || capabilities.HasDrag, "Drag", config);
            valid &= ValidateCapability(!config.requireAlternateModifier || capabilities.HasAlternateModifier, "AlternateModifier", config);

            if (rootConfig.history != null && rootConfig.history.enableUndoRedo)
            {
                valid &= ValidateCapability(!config.allowUndoInput || capabilities.HasUndo, "Undo", config);
                valid &= ValidateCapability(!config.allowRedoInput || capabilities.HasRedo, "Redo", config);
            }

            return valid || !config.strictInputValidation;
        }

        private bool ValidateCapability(bool condition, string capabilityName, TmbsInputConfig config)
        {
            if (condition)
                return true;

            string message = $"TMBS: input adapter no cumple capacidad requerida: {capabilityName}.";

            if (config.strictInputValidation)
                Debug.LogError(message, this);
            else
                Debug.LogWarning(message, this);

            return false;
        }

        private void OnEnable()
        {
            if (!TryValidateSerializedDependencies(out var resolvedInput))
            {
                enabled = false;
                return;
            }

            var composition = new TmbsCompositionRoot();
            composition.Compose(this, rootConfig, instanceId, resolvedInput, targetTilemap, previewTilemap, null,
                out _input, out _pipeline, out _events, out _focus, out _history, out _metadata, out _preview, out _tileSelectionState, out _executor, out _previewEvaluator, out _activeMode);
            
            if (_events != null)
            {
                _events.Subscribe<BuildSelectionChangedEvent>(OnBuildSelectionChanged);
            }

            if (_input != null)
            {
                _input.BuildIntentRaised += OnBuildIntent;
                _input.Enable();
            }
            else if (rootConfig.input != null && rootConfig.input.mode != TmbsInputMode.None)
            {
                Debug.LogError("TMBS: Input Adapter no asignado o inválido.");
            }

            if (rootConfig != null && rootConfig.buildTile != null && _events != null)
            {
                var initial = new BuildSelectionData(
                    rootConfig.buildTile,
                    rootConfig.previewValidTile,
                    rootConfig.previewInvalidTile
                );

                _events.Publish(new BuildSelectionChangedEvent(instanceId, initial));
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.BuildIntentRaised -= OnBuildIntent;
                _input.Disable();
            }

            _preview?.Hide();

            if (rootConfig != null &&
                rootConfig.history != null &&
                rootConfig.history.clearOnDisable)
            {
                _history?.Clear();
            }
        }

        private void OnBuildSelectionChanged(BuildSelectionChangedEvent evt)
        {
            if (evt.InstanceId != instanceId) return;

            _tileSelectionState.UpdateFromSelection(evt.Selection);

            _preview.UpdateTiles(
                evt.Selection.ResolvedPreviewValid,
                evt.Selection.ResolvedPreviewInvalid
            );
        }

        private void OnBuildIntent(BuildIntent intent)
        {
            if (intent.Type == BuildIntentType.Undo)
            {
                _pipeline?.CancelActiveOperation();
                _preview?.Hide();

                if (rootConfig.history != null && rootConfig.history.enableUndoRedo)
                    _history?.TryUndo();

                return;
            }

            if (intent.Type == BuildIntentType.Redo)
            {
                _pipeline?.CancelActiveOperation();
                _preview?.Hide();

                if (rootConfig.history != null && rootConfig.history.enableUndoRedo)
                    _history?.TryRedo();

                return;
            }

            if (IsMutating(intent.Type) && !_focus.CanConsumeMutating(instanceId, intent))
                return;

            var ctx = _pipeline.Process(instanceId, in intent);

            if (intent.Type == BuildIntentType.Cancel)
            {
                _preview?.Hide();
                return;
            }

            if (intent.Type != BuildIntentType.Confirm)
            {
                if (_previewEvaluator.ShouldShowPreview(_activeMode))
                {
                    if (ctx.HasDragBounds)
                    {
                        if (ctx.Feedback.HasBlockedCells)
                        {
                            _preview.ShowRectMasked(ctx.DragBounds, ctx.Feedback.BlockedMask);
                        }
                        else
                        {
                            _preview.ShowRect(ctx.DragBounds, ctx.QuickValidation.IsValid);
                        }
                    }
                    else
                    {
                        var single = new BoundsInt(ctx.Cell, Vector3Int.one);
                        if (ctx.Feedback.HasBlockedCells)
                        {
                            _preview.ShowRectMasked(single, ctx.Feedback.BlockedMask);
                        }
                        else
                        {
                            _preview.ShowCell(ctx.Cell, ctx.QuickValidation.IsValid);
                        }
                    }
                }
                else
                {
                    _preview.Hide();
                }

                if (!ctx.QuickValidation.IsValid)
                {
                    var bounds = ctx.HasDragBounds ? ctx.DragBounds : new BoundsInt(ctx.Cell, Vector3Int.one);
                    _events.Publish(new ValidationFailedEvent(instanceId, ctx.QuickValidation.Failure, ctx.Cell, bounds, ctx.Feedback.BlockedMask));
                }
                return;
            }

            if (!ctx.FullValidation.IsValid)
            {
                var bounds = ctx.HasDragBounds ? ctx.DragBounds : new BoundsInt(ctx.Cell, Vector3Int.one);
                _events.Publish(new ValidationFailedEvent(instanceId, ctx.FullValidation.Failure, ctx.Cell, bounds, ctx.FullValidation.Feedback.BlockedMask));
                return;
            }

            if (ctx.Decision.Type == Core.Execution.ExecutionDecisionType.ExecuteImmediate)
            {
                _executor.Execute(in ctx, targetTilemap);
                _preview.Hide();
            }
        }

        private static bool IsMutating(BuildIntentType type)
        {
            return type == BuildIntentType.Confirm;
        }
    }
}
