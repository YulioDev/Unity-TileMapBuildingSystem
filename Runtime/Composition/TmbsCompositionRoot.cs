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
            out IBuildableSelectionService selectionService,
            out ImmediateBuildExecutor executor,
            out PreviewPolicyEvaluator previewEvaluator,
            out IBuildMode activeMode)
        {
            input = inputAdapter;
            events = new SimpleEventBus();
            focus = new AlwaysFocusService();
            history = new UndoRedoHistory { Capacity = config.historyCapacity };
            metadata = new DefaultMetadataStore(config.metadataCapacity);

            IGridSpace gridSpace = new UnityGridSpace(targetTilemap);

            var catalog = new DefaultBuildableCatalog();
            if (config.buildTile != null)
            {
                catalog.Register(new BuildableDefinition(1, "DefaultWall", config.buildTile));
            }

            selectionService = new DefaultBuildableSelectionService();
            if (config.buildTile != null)
            {
                selectionService.Select(1);
            }

            activeMode = new ImmediateBuildMode();

            previewEvaluator = new PreviewPolicyEvaluator(config.previewPolicy);

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

                    validators.Add(v);
                }
            }

            var validatorPipeline = new ValidatorPipeline(validators);
            var router = new ImmediateOnlyRouter();

            var steps = new List<IPipelineStep>
            {
                new ModeInterpretationStep(activeMode),
                new SelectionGateStep(selectionService)
            };

            pipeline = new BuildPipeline(gridSpace, validatorPipeline, router, steps);

            preview = new TilemapPreviewRenderer(previewTilemap, config.previewValidTile, config.previewInvalidTile);

            var builder = new TileArrayBuilder();
            var writer = new TilemapBatchWriter(builder);

            executor = new ImmediateBuildExecutor(writer, metadata, history, events, catalog, builder);
        }
    }
}

