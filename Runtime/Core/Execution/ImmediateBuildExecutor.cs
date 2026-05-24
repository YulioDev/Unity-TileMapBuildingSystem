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
        private readonly IMetadataStore _metadata;
        private readonly IUndoRedoHistory _history;
        private readonly IEventBus _events;
        private readonly bool _emitRegionModifiedEvents;

        public ImmediateBuildExecutor(
            IMetadataStore metadata,
            IUndoRedoHistory history,
            IEventBus events,
            bool emitRegionModifiedEvents)
        {
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

            var changes = new TileChange[trueCount];
            int changeIndex = 0;
            var beforeTiles = targetTilemap.GetTilesBlock(bounds);

            for (int z = 0; z < bounds.size.z; z++)
            {
                for (int y = 0; y < bounds.size.y; y++)
                {
                    for (int x = 0; x < bounds.size.x; x++)
                    {
                        var cell = bounds.position + new Vector3Int(x, y, z);
                        
                        if (writeMask.Bits[writeMask.IndexOf(cell)])
                        {
                            int blockIndex = TMBS.Core.Grid.TileBlockIndex.IndexOf(bounds, cell);
                            var beforeTile = beforeTiles[blockIndex];
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

                            changes[changeIndex++] = new TileChange(cell, beforeTile, afterTile, beforeMeta, afterMeta);
                        }
                    }
                }
            }

            var instanceId = ctx.InstanceId;

            var command = new PlaceTilesCommand(
                targetTilemap,
                changes,
                _metadata,
                bounds,
                _emitRegionModifiedEvents ? (b) => NotifyRegionModified(instanceId, b) : (Action<BoundsInt>)null);

            if (_history != null)
            {
                _history.Push(command);
            }
            else
            {
                command.Execute();
            }
        }

        private void NotifyRegionModified(string instanceId, BoundsInt bounds)
        {
            _events?.Publish(new RegionModifiedEvent(instanceId, bounds));
        }
    }
}