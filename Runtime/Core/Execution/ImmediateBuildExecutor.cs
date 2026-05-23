using System;
using System.Collections.Generic;
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
        private readonly TilemapBatchWriter _writer;
        private readonly IMetadataStore _metadata;
        private readonly IUndoRedoHistory _history;
        private readonly IEventBus _events;
        private readonly bool _emitRegionModifiedEvents;

        public ImmediateBuildExecutor(
            TilemapBatchWriter writer,
            IMetadataStore metadata,
            IUndoRedoHistory history,
            IEventBus events,
            bool emitRegionModifiedEvents)
        {
            _writer = writer;
            _metadata = metadata;
            _history = history;
            _events = events;
            _emitRegionModifiedEvents = emitRegionModifiedEvents;
        }

        public void Execute(in PipelineContext ctx, Tilemap targetTilemap)
        {
            if (targetTilemap == null)
            {
                Debug.LogError("TMBS: Cannot execute build. targetTilemap is null.");
                return;
            }

            var bounds = ctx.HasDragBounds ? ctx.DragBounds : new BoundsInt(ctx.Cell, Vector3Int.one);
            var writeMask = ctx.Decision.WriteMask ?? TMBS.Core.Validation.CellMask.AllTrue(bounds);

            if (!writeMask.AnyTrue())
                return;
            
            var expectedSize = writeMask.Bounds.size;
            if (bounds.size != expectedSize)
            {
                Debug.LogError("TMBS: WriteMask bounds mismatch. Aborting execution to prevent corrupted writes.");
                return;
            }

            int trueCount = 0;
            for (int i = 0; i < writeMask.Bits.Length; i++)
            {
                if (writeMask.Bits[i])
                {
                    trueCount++;
                }
            }

            var changesList = new List<TileChange>(trueCount);

            for (int z = 0; z < bounds.size.z; z++)
            {
                for (int y = 0; y < bounds.size.y; y++)
                {
                    for (int x = 0; x < bounds.size.x; x++)
                    {
                        var cell = bounds.position + new Vector3Int(x, y, z);
                        
                        if (writeMask.Bits[writeMask.IndexOf(cell)])
                        {
                            var beforeTile = targetTilemap.GetTile(cell);
                            var afterTile = ctx.AlternateBehaviour ? ctx.Decision.AlternateWriteTile : ctx.Decision.WriteTile;

                            BuildRecord? beforeMeta = null;
                            if (_metadata != null && _metadata.TryGet(cell, out var record))
                            {
                                beforeMeta = record;
                            }

                            BuildRecord? afterMeta = null;
                            if (afterTile != null && ctx.Decision.ConstructionId > 0)
                            {
                                afterMeta = new BuildRecord(
                                    afterTile,
                                    cell,
                                    BuildState.Completed);
                            }

                            changesList.Add(new TileChange(cell, beforeTile, afterTile, beforeMeta, afterMeta));
                        }
                    }
                }
            }

            var changes = changesList.ToArray();
            var instanceId = ctx.InstanceId;

            var command = new PlaceTilesCommand(
                targetTilemap,
                _writer,
                changes,
                _metadata,
                bounds,
                _emitRegionModifiedEvents ? (b) => NotifyRegionModified(instanceId, b) : (Action<BoundsInt>)null);

            _history?.Push(command);
        }

        private void NotifyRegionModified(string instanceId, BoundsInt bounds)
        {
            _events?.Publish(new RegionModifiedEvent(instanceId, bounds));
        }
    }
}