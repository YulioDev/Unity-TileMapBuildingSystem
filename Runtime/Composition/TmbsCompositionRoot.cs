using System.Collections.Generic;
using TMBS.Core.Catalog;
using TMBS.Core.Events;
using TMBS.Core.Execution;
using TMBS.Core.Focus;
using TMBS.Core.Grid;
using TMBS.Core.History;
using TMBS.Core.Input;
using TMBS.Core.Metadata;
using TMBS.Core.Modes;
using TMBS.Core.Pipeline;
using TMBS.Core.Pipeline.Steps;
using TMBS.Core.Preview;
using TMBS.Core.Selection;
using TMBS.Core.Validation;
using TMBS.Runtime.Config;
using TMBS.Unity.GridSpaces;
using TMBS.Unity.Preview;
using TMBS.Unity.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Runtime.Facade
{
    public sealed class TmbsCompositionRoot
    {
        public void Compose(
            BuildeableTilemap facade,
            TmbsRootConfig config,
            string instanceId,
            IBuildInputAdapter inputAdapter,
            Tilemap targetTilemap,
            Tilemap previewTilemap,
            List<MonoBehaviour> sceneValidators,
            out IBuildInputAdapter input,
            out IBuildPipeline pipeline,
            out IEventBus events,
            out IInputFocusService focus,
            out IUndoRedoHistory history,
            out IMetadataStore metadata,
            out IPreviewRenderer preview,
            out TileSelectionState selectionState,
            out ImmediateBuildExecutor executor,
            out PreviewPolicyEvaluator previewEvaluator,
            out IBuildMode activeMode)
        {
            input = inputAdapter;
            events = new SimpleEventBus();
            focus = new AlwaysFocusService();
            int histCap = config.historyCapacity > 0 ? config.historyCapacity : 50;
            int metaCap = config.metadataCapacity > 0 ? config.metadataCapacity : 1000;
            history = new UndoRedoHistory { Capacity = histCap };
            metadata = new DefaultMetadataStore(metaCap);

            IGridSpace gridSpace = new UnityGridSpace(targetTilemap);

            var catalog = new DefaultBuildableCatalog();
            if (config.buildTile != null)
            {
                catalog.Register(new BuildableDefinition(1, "DefaultWall", config.buildTile));
            }

            var tileSelectionState = new TileSelectionState();
            selectionState = tileSelectionState;

            activeMode = new ImmediateBuildMode();

            previewEvaluator = new PreviewPolicyEvaluator(config.previewPolicy);

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

            var validatorPipeline = new ValidatorPipeline(validators);
            var router = new ImmediateOnlyRouter();

            var steps = new List<IPipelineStep>
            {
                new TileInjectionStep(tileSelectionState),
                new ModeInterpretationStep(activeMode)
            };

            pipeline = new BuildPipeline(gridSpace, validatorPipeline, router, steps);

            preview = new TilemapPreviewRenderer(previewTilemap, config.previewValidTile, config.previewInvalidTile);

            var builder = new TileArrayBuilder();
            var batchWriter = new TilemapBatchWriter();
            var writeStrategy = new HybridTilemapWriteStrategy(batchWriter, config.sparseWriteDenseThreshold);

            executor = new ImmediateBuildExecutor(writeStrategy, metadata, history, events, builder, config.sparseWriteDenseThreshold);
        }
    }
}

