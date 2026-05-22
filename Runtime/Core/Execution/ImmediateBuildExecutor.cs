using TMBS.Core.Catalog;
using TMBS.Core.Events;
using TMBS.Core.History;
using TMBS.Core.Metadata;
using TMBS.Core.Pipeline;
using TMBS.Core.Validation;
using TMBS.Unity.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace TMBS.Core.Execution
{
    public sealed class ImmediateBuildExecutor
    {
        private readonly ITilemapWriteStrategy _writer;
        private readonly IMetadataStore _metadata;
        private readonly IUndoRedoHistory _history;
        private readonly IEventBus _events;
        private readonly TileArrayBuilder _builder;
        private readonly float _sparseThreshold;

        public ImmediateBuildExecutor(
            ITilemapWriteStrategy writer,
            IMetadataStore metadata,
            IUndoRedoHistory history,
            IEventBus events,
            TileArrayBuilder builder,
            float sparseThreshold = 0.65f)
        {
            _writer = writer;
            _metadata = metadata;
            _history = history;
            _events = events;
            _builder = builder;
            _sparseThreshold = sparseThreshold;
        }

        public void Execute(in PipelineContext ctx, Tilemap targetTilemap)
        {
            if (targetTilemap == null) return;
            if (!ctx.HasSelection && !ctx.AlternateBehaviour) return;

            TileBase targetTile = null;

            if (!ctx.AlternateBehaviour)
            {
                targetTile = ctx.SelectedTile;
                if (targetTile == null) return;
            }

            var bounds = ctx.HasDragBounds
                ? ctx.DragBounds
                : new BoundsInt(ctx.Cell, new Vector3Int(1, 1, 1));

            var writeMask = ctx.FullValidation.WriteMask;
            bool isSparse = false;
            
            if (writeMask != null)
            {
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

                int writable = 0;
                for (int i = 0; i < writeMask.Bits.Length; i++)
                {
                    if (writeMask.Bits[i]) writable++;
                }
                float density = writable / (float)writeMask.Bits.Length;
                isSparse = density < _sparseThreshold;
            }

            TileBase[] beforeTiles = null;
            TileBase[] afterTiles = null;
            BuildRecord?[] beforeMeta = null;
            BuildRecord?[] afterMeta = null;
            TileChange[] changes = null;

            int len = bounds.size.x * bounds.size.y * bounds.size.z;

            if (isSparse)
            {
                var changeList = new List<TileChange>();
                for (int i = 0; i < writeMask.Bits.Length; i++)
                {
                    if (!writeMask.Bits[i]) continue;

                    Vector3Int cell = writeMask.CellAt(i);
                    TileBase before = targetTilemap.GetTile(cell);
                    TileBase after = targetTile;
                    
                    BuildRecord? bMeta = null;
                    BuildRecord? aMeta = null;

                    if (_metadata != null)
                    {
                        if (_metadata.TryGet(cell, out var rec))
                            bMeta = rec;
                            
                        if (after != null)
                            aMeta = new BuildRecord(ctx.SelectedTile, cell, BuildState.Completed);
                    }

                    changeList.Add(new TileChange(cell, before, after, bMeta, aMeta));
                }
                changes = changeList.ToArray();
            }
            else
            {
                beforeTiles = _builder.ReadBlock(targetTilemap, bounds);
                afterTiles = _builder.FillBlock(bounds, targetTile);

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
                            : TMBS.Core.Grid.TileBlockIndex.CellAt(bounds, i);

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
                            afterMeta[i] = null;
                        else
                            afterMeta[i] = new BuildRecord(ctx.SelectedTile, cell, BuildState.Completed);
                    }
                }
            }

            string instanceId = ctx.InstanceId;
            var cmd = new PlaceTilesCommand(
                _writer,
                _metadata,
                targetTilemap,
                bounds,
                beforeTiles,
                afterTiles,
                beforeMeta,
                afterMeta,
                changes,
                isSparse,
                b => _events.Publish(new RegionModifiedEvent(instanceId, b))
            );

            _history.Push(cmd);
        }
    }
}