using System.Collections.Generic;
using TMBS.Core.Events;
using TMBS.Core.Execution;
using TMBS.Core.Focus;
using TMBS.Core.Grid;
using TMBS.Core.History;
using TMBS.Core.Input;
using TMBS.Core.Metadata;
using TMBS.Core.Modes;
using TMBS.Core.Pending;
using TMBS.Core.Pipeline;
using TMBS.Core.Preview;
using TMBS.Core.Selection;
using TMBS.Core.Validation;
using TMBS.Runtime.Config;
using TMBS.Unity.GridSpaces;
using TMBS.Unity.Preview;
using TMBS.Unity.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Runtime.Interface
{
    public sealed class TmbsCompositionRoot
    {
        public TmbsRuntimeContext Compose(
            TmbsRootConfig config,
            string instanceId,
            IBuildInputAdapter inputAdapter,
            Tilemap targetTilemap,
            Tilemap previewTilemap,
            List<MonoBehaviour> sceneValidators)
        {
            var input = inputAdapter;
            var events = new SimpleEventBus();
            var focus = (IInputFocusService)new TMBS.Unity.Input.UiFocusGuardService();
            
            var historyConfig = config.GetRuntimeHistoryConfig();
            IUndoRedoHistory history;
            if (historyConfig.enableUndoRedo)
            {
                history = new UndoRedoHistory { Capacity = historyConfig.capacity };
            }
            else
            {
                history = new NoUndoRedoHistory { Capacity = historyConfig.capacity };
            }

            int metaCap = config.GetRuntimeMetadataInitialCapacity();
            var metadata = new DefaultMetadataStore(metaCap);

            IGridSpace gridSpace = new UnityGridSpace(targetTilemap);

            var tileSelectionState = new TileSelectionState();
            var selectionState = tileSelectionState;

            IBuildMode activeMode = new ImmediateBuildMode();
            IExecutionRouter router;
            IBuildExecutor executor;
            IPendingConstructionStore pendingStore = null;
            IPendingConstructionWorkApi pendingWorkApi = null;
            PendingDebugBuilder pendingDebugBuilder = null;
            BoundsInt? globalBounds = null;

            switch (config.executionMode)
            {
                case ExecutionMode.Pending:
                {
                    var pendingConfig = config.GetRuntimePendingConfig();
                    pendingStore = new DefaultPendingConstructionStore();
                    router = new PendingBuildRouter();
                    executor = new PendingBuildExecutor(pendingStore, events, pendingConfig);
                    pendingWorkApi = new DefaultPendingConstructionWorkApi(
                        pendingStore, events, targetTilemap, metadata, instanceId);

                    var debugConfig = config.GetRuntimePendingDebugConfig();
                    pendingDebugBuilder = debugConfig.enabled
                        ? new PendingDebugBuilder(pendingWorkApi, debugConfig)
                        : null;
                    break;
                }
                case ExecutionMode.Immediate:
                default:
                    router = new ImmediateOnlyRouter();
                    executor = new ImmediateBuildExecutor(metadata, history, events, historyConfig.emitRegionModifiedEvents);
                    break;
            }

            var previewEvaluator = new PreviewPolicyEvaluator(config.previewPolicy);

            var occupancySource = new UnityTilemapOccupancySource(targetTilemap);

            var validators = new List<IValidator>();

            if (config.validators != null)
            {
                for (int i = 0; i < config.validators.Count; i++)
                {
                    var entry = config.validators[i];
                    if (entry == null)
                        continue;

                    if (!entry.enabled)
                        continue;

                    var v = entry.validator;
                    if (v == null)
                        continue;

                    if (v is BoundsValidator bv)
                    {
                        globalBounds = bv.allowedBounds;
                    }

                    if (v is IInjectableValidator injectable)
                    {
                        injectable.Inject(metadata);
                    }
                    if (v is IOccupancySourceInjectable occInjectable)
                    {
                        occInjectable.InjectOccupancySource(occupancySource);
                    }

                    validators.Add(v);
                }
            }

            if (sceneValidators != null)
            {
                for (int i = 0; i < sceneValidators.Count; i++)
                {
                    if (sceneValidators[i] is IValidator validator)
                    {
                        if (validator is IInjectableValidator injectable)
                            injectable.Inject(metadata);

                        if (validator is IOccupancySourceInjectable occInjectable)
                            occInjectable.InjectOccupancySource(occupancySource);

                        validators.Add(validator);
                    }
                }
            }

            if (pendingStore != null)
            {
                for (int i = 0; i < validators.Count; i++)
                    if (validators[i] is IPendingStoreInjectable si)
                        si.InjectStore(pendingStore);
            }

            var validatorPipeline = new ValidatorPipeline(validators);

            var pipeline = new BuildPipeline(
                gridSpace, 
                validatorPipeline, 
                router, 
                tileSelectionState, 
                activeMode, 
                config.GetRuntimeClampDragBoundsToBoundsValidator(), 
                globalBounds);

            var overlay = config.previewOverlay ?? new TmbsPreviewOverlayConfig();
            var preview = new TilemapPreviewRenderer(previewTilemap, overlay.validColor, overlay.invalidColor);

            return new TmbsRuntimeContext(
                input,
                pipeline,
                events,
                focus,
                history,
                metadata,
                preview,
                selectionState,
                executor,
                previewEvaluator,
                activeMode,
                pendingWorkApi,
                pendingDebugBuilder);
        }
    }
}