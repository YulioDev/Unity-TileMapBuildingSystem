using TMBS.Core.Catalog;
using TMBS.Core.Events;
using TMBS.Core.History;
using TMBS.Core.Metadata;
using TMBS.Core.Pipeline;
using TMBS.Core.Validation;
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
            int buildableId = 0;

            if (!ctx.AlternateBehaviour)
            {
                buildableId = ctx.SelectedBuildableId;
                if (!_catalog.TryGet(buildableId, out var definition)) return;
                targetTile = definition.VisualTile;
            }

            var bounds = ctx.HasDragBounds
                ? ctx.DragBounds
                : new BoundsInt(ctx.Cell, new Vector3Int(1, 1, 1));

            var writeMask = ctx.FullValidation.WriteMask;
            if (writeMask != null)
            {
                // Contrato: la máscara debe corresponder al mismo bounds que vamos a ejecutar.
                if (writeMask.Bounds.position != bounds.position || writeMask.Bounds.size != bounds.size)
                {
                    Debug.LogError("TMBS: WriteMask bounds mismatch. Aborting execution to prevent corrupted writes.");
                    return;
                }
                int expectedLen = bounds.size.x * bounds.size.y * bounds.size.z;
                if (writeMask.Bits == null || writeMask.Bits.Length != expectedLen)
                {
                    Debug.LogError("TMBS: WriteMask length mismatch. Aborting execution.");
                    return;
                }
            }

            var beforeTiles = _builder.ReadBlock(targetTilemap, bounds);
            var afterTiles = _builder.FillBlock(bounds, targetTile);

            BuildRecord?[] beforeMeta = null;
            BuildRecord?[] afterMeta = null;

            int len = afterTiles.Length;

            if (writeMask != null)
            {
                for (int i = 0; i < len; i++)
                {
                    if (!writeMask.Bits[i])
                    {
                        afterTiles[i] = beforeTiles[i];
                    }
                }
            }

            if (_metadata != null)
            {
                beforeMeta = new BuildRecord?[len];
                afterMeta = new BuildRecord?[len];

                for (int i = 0; i < len; i++)
                {
                    Vector3Int cell = (writeMask != null)
                        ? writeMask.CellAt(i)
                        : new Vector3Int(
                            bounds.position.x + (i % bounds.size.x),
                            bounds.position.y + ((i / bounds.size.x) % bounds.size.y),
                            bounds.position.z + (i / (bounds.size.x * bounds.size.y))
                          );

                    if (_metadata.TryGet(cell, out var rec))
                        beforeMeta[i] = rec;
                    else
                        beforeMeta[i] = null;

                    bool canWrite = writeMask == null || writeMask.Bits[i];

                    if (!canWrite)
                    {
                        afterMeta[i] = beforeMeta[i];
                        continue;
                    }

                    if (afterTiles[i] == null)
                    {
                        afterMeta[i] = null;
                    }
                    else
                    {
                        afterMeta[i] = new BuildRecord(buildableId, cell, BuildState.Completed);
                    }
                }
            }

            var cmd = new PlaceTilesCommand(
                _writer,
                _metadata,
                targetTilemap,
                bounds,
                beforeTiles,
                afterTiles,
                beforeMeta,
                afterMeta
            );

            _history.Push(cmd);
            _events.Publish(new RegionModifiedEvent(ctx.InstanceId, bounds));
        }
    }
}