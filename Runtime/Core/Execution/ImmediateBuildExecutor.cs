using TMBS.Core.Catalog;
using TMBS.Core.Events;
using TMBS.Core.History;
using TMBS.Core.Metadata;
using TMBS.Core.Pipeline;
using TMBS.Unity.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Execution
{
    public sealed class ImmediateBuildExecutor
    {
        private readonly ITilemapBatchWriter _writer;
        private readonly IMetadataStore _metadata;
        private readonly IUndoRedoHistory _history;
        private readonly IEventBus _events;
        private readonly IBuildableCatalog _catalog;
        private readonly TileArrayBuilder _builder;

        public ImmediateBuildExecutor(
            ITilemapBatchWriter writer, 
            IMetadataStore metadata, 
            IUndoRedoHistory history, 
            IEventBus events,
            IBuildableCatalog catalog,
            TileArrayBuilder builder)
        {
            _writer = writer;
            _metadata = metadata;
            _history = history;
            _events = events;
            _catalog = catalog;
            _builder = builder;
        }

        public void Execute(in PipelineContext ctx, Tilemap targetTilemap)
        {
            if (targetTilemap == null) return;
            
            
            if (!ctx.HasSelection && !ctx.AlternateBehaviour) return;

            TileBase targetTile = null;
            if (!ctx.AlternateBehaviour)
            {
                if (!_catalog.TryGet(ctx.SelectedBuildableId, out var definition))
                    return; 
                targetTile = definition.VisualTile;
            }

            var bounds = ctx.HasDragBounds 
                ? ctx.DragBounds 
                : new BoundsInt(ctx.Cell, new Vector3Int(1, 1, 1));

            var before = _builder.ReadBlock(targetTilemap, bounds);
            
            
            var after = _builder.FillBlock(bounds, targetTile);

            var cmd = new PlaceTilesCommand(_writer, _metadata, targetTilemap, bounds, before, after, ctx.AlternateBehaviour ? 0 : ctx.SelectedBuildableId);
            _history.Push(cmd);
            
            _events.Publish(new RegionModifiedEvent(ctx.InstanceId, bounds));
        }
    }
}

