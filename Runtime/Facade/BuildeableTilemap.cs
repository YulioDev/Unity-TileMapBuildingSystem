using System;
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
        [Tooltip("The core configuration asset that defines rules, input, and performance settings.")]
        [SerializeField] private TmbsRootConfig rootConfig;
        
        [Tooltip("Unique identifier for this building instance. Used to route events correctly.")]
        [SerializeField] private string instanceId = "TMBS_Instance";

        [Header("Scene References")]
        [Tooltip("The main tilemap where tiles will be placed.")]
        [SerializeField] private Tilemap targetTilemap;
        
        [Tooltip("A temporary tilemap used to render build previews and validation feedback.")]
        [SerializeField] private Tilemap previewTilemap;

        [Tooltip("The world camera used to process pointer input. Overrides Camera.main if the root config policy allows it.")]
        [SerializeField] private Camera worldCamera;

        [Header("Discovery")]
        [Tooltip("If enabled, any IValidator component attached to this GameObject will be automatically included in the validation pipeline.")]
        [SerializeField] private bool includeAttachedSceneValidators = false;

        private enum FacadeState
        {
            NotComposed,
            Composing,
            Composed,
            Disabled
        }

        private FacadeState _state = FacadeState.NotComposed;

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

        private IBuildInputAdapter _externalInputAdapter;
        private IBuildInputAdapterProvider _externalInputProvider;
        private bool _externalInputCreatedByProvider;
        private bool _inputCreatedInternally;

        private void Update()
        {
            if (_input is ITickableInputAdapter tickable)
            {
                tickable.Tick(Time.deltaTime);
            }
        }

        private Camera ResolveCamera()
        {
            if (rootConfig == null) return Camera.main;

            if (rootConfig.GetRuntimeCameraMode() == TmbsCameraMode.AlwaysMainCamera)
                return Camera.main;

            if (worldCamera == null)
            {
                Debug.LogWarning("TMBS: Camera Mode is set to UseFacadeReference but worldCamera is missing. Using Camera.main instead.", this);
                return Camera.main;
            }

            return worldCamera;
        }

        public bool SetExternalInputAdapter(IBuildInputAdapter adapter)
        {
            if (_state == FacadeState.Composing || _state == FacadeState.Composed)
            {
                Debug.LogError("TMBS: SetExternalInputAdapter must be called before BuildeableTilemap is enabled.", this);
                return false;
            }

            ReleaseExternalInputIfNeeded();

            _externalInputAdapter = adapter;
            _externalInputCreatedByProvider = false;

            return true;
        }

        public bool SetExternalInputProvider(IBuildInputAdapterProvider provider)
        {
            if (_state == FacadeState.Composing || _state == FacadeState.Composed)
            {
                Debug.LogError("TMBS: SetExternalInputProvider must be called before BuildeableTilemap is enabled.", this);
                return false;
            }

            ReleaseExternalInputIfNeeded();

            _externalInputProvider = provider;
            return true;
        }

        private void ReleaseExternalInputIfNeeded()
        {
            if (_externalInputAdapter == null)
                return;

            if (_externalInputCreatedByProvider && _externalInputProvider != null)
            {
                _externalInputProvider.ReleaseInputAdapter(_externalInputAdapter);
            }
            else if (_externalInputAdapter is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _externalInputAdapter = null;
            _externalInputCreatedByProvider = false;
        }

        private bool TryValidateSerializedDependencies(out IBuildInputAdapter resolvedInput)
        {
            resolvedInput = null;

            if (rootConfig == null)
            {
                Debug.LogError("TMBS: rootConfig is not assigned.", this);
                return false;
            }

            if (targetTilemap == null)
            {
                Debug.LogError("TMBS: targetTilemap is not assigned.", this);
                return false;
            }

            if (previewTilemap == null)
            {
                Debug.LogError("TMBS: previewTilemap is not assigned.", this);
                return false;
            }

            return TryResolveInputAdapter(out resolvedInput);
        }

        private bool TryResolveInputAdapter(out IBuildInputAdapter resolvedInput)
        {
            resolvedInput = null;
            _inputCreatedInternally = false;

            var inputConfig = rootConfig.input;

            if (inputConfig == null || inputConfig.mode == TmbsInputMode.None)
            {
                return true;
            }

            switch (inputConfig.mode)
            {
                case TmbsInputMode.ExternalProvided:
                    resolvedInput = ResolveExternalInputAdapter();
                    break;

                case TmbsInputMode.Mouse:
                    resolvedInput = CreateLegacyMouseInputAdapter();
                    _inputCreatedInternally = true;
                    break;

                default:
                    Debug.LogError($"TMBS: Input mode {inputConfig.mode} is not supported.", this);
                    return false;
            }

            if (resolvedInput == null)
            {
                Debug.LogError($"TMBS: Failed to create input adapter for mode {inputConfig.mode}.", this);
                return false;
            }

            return ValidateInputCapabilities(resolvedInput.Capabilities, inputConfig);
        }

        private IBuildInputAdapter CreateLegacyMouseInputAdapter()
        {
            return new TMBS.Unity.Input.LegacyMouseBuildInputAdapter(ResolveCamera);
        }

        private IBuildInputAdapter ResolveExternalInputAdapter()
        {
            if (_externalInputAdapter != null)
                return _externalInputAdapter;

            if (_externalInputProvider != null &&
                _externalInputProvider.TryCreateInputAdapter(
                    new TMBS.Core.Input.BuildInputAdapterContext(instanceId),
                    out var adapter))
            {
                _externalInputAdapter = adapter;
                _externalInputCreatedByProvider = true;
                return adapter;
            }

            Debug.LogError("TMBS: ExternalProvided requires SetExternalInputAdapter or SetExternalInputProvider to be called before OnEnable.", this);
            return null;
        }

        private string GetSafeObjectName()
        {
            return gameObject != null && !string.IsNullOrEmpty(gameObject.name)
                ? gameObject.name
                : "[Unnamed GameObject]";
        }

        private List<MonoBehaviour> ResolveAttachedSceneValidators()
        {
            if (!includeAttachedSceneValidators)
                return null;

            var behaviours = GetComponents<MonoBehaviour>();
            var validators = new List<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IValidator)
                    validators.Add(behaviours[i]);
            }

            return validators.Count > 0 ? validators : null;
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

            string message = $"TMBS: Input adapter does not meet required capability: {capabilityName}.";

            if (config.strictInputValidation)
                Debug.LogError(message, this);
            else
                Debug.LogWarning(message, this);

            return false;
        }

        private void OnEnable()
        {
            _state = FacadeState.Composing;

            if (!TryValidateSerializedDependencies(out var resolvedInput))
            {
                _state = FacadeState.Disabled;
                enabled = false;
                return;
            }

            var composition = new TmbsCompositionRoot();
            var context = composition.Compose(
                this, 
                rootConfig, 
                instanceId, 
                resolvedInput, 
                targetTilemap, 
                previewTilemap, 
                ResolveAttachedSceneValidators());
                
            _input = context.Input;
            _pipeline = context.Pipeline;
            _events = context.Events;
            _focus = context.Focus;
            _history = context.History;
            _metadata = context.Metadata;
            _preview = context.Preview;
            _tileSelectionState = context.SelectionState;
            _executor = context.Executor;
            _previewEvaluator = context.PreviewEvaluator;
            _activeMode = context.ActiveMode;
            
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
                Debug.LogError("TMBS: Input Adapter is unassigned or invalid.", this);
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

            _state = FacadeState.Composed;
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.BuildIntentRaised -= OnBuildIntent;
                _input.Disable();

                if ((_inputCreatedInternally || _externalInputCreatedByProvider) && 
                    _input is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _input = null;
                _inputCreatedInternally = false;
            }

            _preview?.Hide();

            if (rootConfig != null &&
                rootConfig.history != null &&
                rootConfig.history.clearOnDisable)
            {
                _history?.Clear();
            }

            ReleaseExternalInputIfNeeded();

            _state = FacadeState.Disabled;
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