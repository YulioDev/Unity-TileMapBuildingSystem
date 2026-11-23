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
    public sealed class TmbsFactory
    {
        public TmbsSession Compose(
            TmbsRootConfig config,
            string instanceId,
            IBuildInputAdapter inputAdapter,
            Tilemap targetTilemap,
            Tilemap previewTilemap,
            List<MonoBehaviour> sceneValidators,
            IEventBus existingEvents = null)
        {
            var input = inputAdapter;
            var events = existingEvents ?? new SimpleEventBus();
            var focus = (IInputFocusService)new TMBS.Unity.Input.UiFocusGuard();
            
            var historyConfig = config.GetHistoryConfig();
            IUndoRedoHistory history;
            if (historyConfig.enableUndoRedo)
            {
                history = new UndoRedoHistory { Capacity = historyConfig.capacity };
            }
            else
            {
                history = new NoUndoRedoHistory { Capacity = historyConfig.capacity };
            }

            int metaCap = config.GetRecordCapacity();
            var metadata = new MetadataStore(metaCap);

            IGridSpace gridSpace = new UnityGridSpace(targetTilemap);

            var tileSelectionState = new TileSelectionState();

            IBuildMode activeMode = new ImmediateBuildMode();
            IExecutionRouter router;
            IBuildExecutor executor;
            IPendingStore pendingStore = null;
            IPendingWorkApi pendingWorkApi = null;
            PendingDebugBuilder pendingDebugBuilder = null;
            BoundsInt? globalBounds = null;

            bool allowReplacePending = config.executionMode == ExecutionMode.Pending &&
                                       config.GetPendingConfig().replaceExistingPending;

            switch (config.executionMode)
            {
                case ExecutionMode.Pending:
                {
                    var pendingConfig = config.GetPendingConfig();
                    pendingStore = new PendingStore();
                    router = new PendingBuildRouter();
                    var pendingExecutor = new PendingBuildExecutor(pendingStore, events, pendingConfig, history);
                    pendingWorkApi = new PendingWorkApi(
                        pendingStore, events, targetTilemap, metadata, instanceId,
                        pendingConfig.allowPartialCompletion, pendingConfig.resourcePolicy,
                        pendingConfig.emitPendingEvents, historyConfig.emitRegionEvents);
                    pendingExecutor.InjectWorkApi(pendingWorkApi);
                    executor = pendingExecutor;

                    var debugConfig = config.GetPendingDebugConfig();
                    pendingDebugBuilder = debugConfig.enabled
                        ? new PendingDebugBuilder(pendingWorkApi, debugConfig, pendingConfig.resourcePolicy)
                        : null;
                    break;
                }
                case ExecutionMode.Immediate:
                default:
                    router = new ImmediateOnlyRouter();
                    executor = new ImmediateBuildExecutor(metadata, history, events, historyConfig.emitRegionEvents);
                    break;
            }

            var previewEvaluator = new PreviewToggle(config.previewPolicy);

            var occupancySource = new TilemapOccupancy(targetTilemap);

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

                    InjectValidatorDependencies(v, metadata, occupancySource, pendingStore, allowReplacePending);

                    validators.Add(v);
                }
            }

            if (sceneValidators != null)
            {
                for (int i = 0; i < sceneValidators.Count; i++)
                {
                    if (sceneValidators[i] is IValidator validator)
                    {
                        InjectValidatorDependencies(validator, metadata, occupancySource, pendingStore, allowReplacePending);

                        validators.Add(validator);
                    }
                }
            }

            var validatorPipeline = new ValidatorPipeline(validators);

            var pipeline = new BuildPipeline(
                gridSpace, 
                validatorPipeline, 
                router, 
                tileSelectionState, 
                activeMode, 
                config.ShouldClampToBounds(), 
                globalBounds);

            var overlay = config.previewOverlay ?? new TmbsPreviewConfig();
            var preview = new TilemapPreviewRenderer(previewTilemap, overlay.validColor, overlay.invalidColor);

            return new TmbsSession(
                input,
                pipeline,
                events,
                focus,
                history,
                metadata,
                preview,
                tileSelectionState,
                executor,
                previewEvaluator,
                activeMode,
                pendingWorkApi,
                pendingDebugBuilder);
        }

        private static void InjectValidatorDependencies(
            IValidator validator,
            IMetadataStore metadata,
            ICellOccupancySource occupancySource,
            IPendingStore pendingStore,
            bool allowReplacePending)
        {
            if (validator is IMetadataConsumer injectable)
                injectable.Inject(metadata);

            if (validator is IOccupancyConsumer occInjectable)
                occInjectable.InjectOccupancySource(occupancySource);

            if (pendingStore != null && validator is IPendingStoreConsumer storeConsumer)
                storeConsumer.InjectStore(pendingStore);

            if (validator is PendingOccupancyValidator pendingValidator)
                pendingValidator.AllowReplaceExisting = allowReplacePending;
        }
    }
}
