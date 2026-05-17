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
        private IBuildableSelectionService _selectionService;
        private ImmediateBuildExecutor _executor;
        private PreviewPolicyEvaluator _previewEvaluator;
        private IBuildMode _activeMode;

        private void OnEnable()
        {
            var composition = new TmbsCompositionRoot();
            composition.Compose(this, rootConfig, instanceId, inputAdapter as IBuildInputAdapter, targetTilemap, previewTilemap, null,
                out _input, out _pipeline, out _events, out _focus, out _history, out _metadata, out _preview, out _selectionService, out _executor, out _previewEvaluator, out _activeMode);
            
            if (_input != null)
            {
                _input.BuildIntentRaised += OnBuildIntent;
                _input.Enable();
            }
            else
            {
                Debug.LogError("TMBS: Input Adapter is no asignado o no implementa IBuildInputAdapter.");
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
        }

        private void OnBuildIntent(BuildIntent intent)
        {
            if (IsMutating(intent.Type) && !_focus.CanConsumeMutating(instanceId, intent))
                return;

            var ctx = _pipeline.Process(instanceId, in intent);

            if (intent.Type != BuildIntentType.Confirm)
            {
                if (_previewEvaluator.ShouldShowPreview(_activeMode))
                {
                    if (ctx.HasDragBounds)
                    {
                        _preview.ShowRect(ctx.DragBounds, ctx.QuickValidation.IsValid);
                    }
                    else
                    {
                        _preview.ShowCell(ctx.Cell, ctx.QuickValidation.IsValid);
                    }
                }
                else
                {
                    _preview.Hide();
                }

                if (!ctx.QuickValidation.IsValid)
                    _events.Publish(new ValidationFailedEvent(instanceId, ctx.QuickValidation.Failure, ctx.Cell));
                return;
            }

            if (!ctx.FullValidation.IsValid)
            {
                _events.Publish(new ValidationFailedEvent(instanceId, ctx.FullValidation.Failure, ctx.Cell));
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

