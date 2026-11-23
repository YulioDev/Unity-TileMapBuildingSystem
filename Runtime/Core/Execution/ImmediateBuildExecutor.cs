using System;
using System.Collections.Generic;
using TMBS.Core.Events;
using TMBS.Core.History;
using TMBS.Core.Metadata;
using TMBS.Core.Pipeline;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Execution
{
    public sealed class ImmediateBuildExecutor : IBuildExecutor
    {
        private readonly IMetadataStore _metadata;
        private readonly IUndoRedoHistory _history;
        private readonly IEventBus _events;
        private readonly bool _emitRegionEvents;

        public ImmediateBuildExecutor(
            IMetadataStore metadata,
            IUndoRedoHistory history,
            IEventBus events,
            bool emitRegionEvents)
        {
            _metadata = metadata;
            _history = history;
            _events = events;
            _emitRegionEvents = emitRegionEvents;
        }

        public void Execute(in PipelineContext ctx, Tilemap targetTilemap)
        {
            if (targetTilemap == null)
            {
                Debug.LogError("TMBS: Cannot execute build. targetTilemap is null.");
                return;
            }

            var bounds = ctx.HasDragBounds ? ctx.DragBounds : new BoundsInt(ctx.Cell, Vector3Int.one);
            var writeMask = ctx.Decision.WriteMask ?? TMBS.Core.Validation.CellMaskPool.Rent(bounds, true);

            if (!writeMask.AnyTrue())
                return;
            
            var expectedSize = writeMask.Bounds.size;
            if (bounds.size != expectedSize)
            {
                Debug.LogError("TMBS: WriteMask bounds mismatch. Aborting execution to prevent corrupted writes.");
                return;
            }

            var changeList = new List<TileChange>(writeMask.Length);
            var effectiveBounds = bounds;

            if (ctx.AlternateBehaviour)
            {
                var eraseCells = new HashSet<Vector3Int>();
                for (int i = 0; i < writeMask.Length; i++)
                {
                    if (!writeMask.Bits[i])
                        continue;

                    var cell = writeMask.CellAt(i);
                    eraseCells.Add(cell);

                    if (_metadata != null && _metadata.TryGet(cell, out var existingRec) &&
                        (existingRec.FootprintBounds.size.x > 1 || existingRec.FootprintBounds.size.y > 1))
                    {
                        foreach (var bpPos in existingRec.FootprintBounds.allPositionsWithin)
                            eraseCells.Add(bpPos);
                    }
                }

                if (eraseCells.Count > 0)
                {
                    Vector3Int min = bounds.min;
                    Vector3Int max = bounds.max;
                    bool hasBounds = false;

                    foreach (var cell in eraseCells)
                    {
                        var beforeTile = targetTilemap.GetTile(cell);
                        BuildRecord? beforeMeta = null;
                        if (_metadata != null && _metadata.TryGet(cell, out var record))
                            beforeMeta = record;

                        if (beforeTile == null && !beforeMeta.HasValue)
                            continue;

                        var beforeMatrix = targetTilemap.GetTransformMatrix(cell);
                        var afterTile = ctx.Decision.AlternateWriteTile;

                        changeList.Add(new TileChange(cell, beforeTile, afterTile, beforeMeta, null, beforeMatrix, Matrix4x4.identity));

                        if (!hasBounds)
                        {
                            min = cell;
                            max = cell + Vector3Int.one;
                            hasBounds = true;
                        }
                        else
                        {
                            min = Vector3Int.Min(min, cell);
                            max = Vector3Int.Max(max, cell + Vector3Int.one);
                        }
                    }

                    if (hasBounds)
                        effectiveBounds = new BoundsInt(min, max - min);
                }
            }
            else
            {
                var beforeTiles = targetTilemap.GetTilesBlock(bounds);
                bool isBlueprint = ctx.BuildableData != null && ctx.BuildableData.isBlueprint;

                for (int z = 0; z < bounds.size.z; z++)
                {
                    for (int y = 0; y < bounds.size.y; y++)
                    {
                        for (int x = 0; x < bounds.size.x; x++)
                        {
                            var cell = bounds.position + new Vector3Int(x, y, z);

                            if (!writeMask.Bits[writeMask.IndexOf(cell)])
                                continue;

                            int blockIndex = TMBS.Core.Grid.TileBlockIndex.IndexOf(bounds, cell);
                            var beforeTile = beforeTiles[blockIndex];

                            BuildRecord? beforeMeta = null;
                            if (_metadata != null && _metadata.TryGet(cell, out var record))
                                beforeMeta = record;

                            var beforeMatrix = targetTilemap.GetTransformMatrix(cell);
                            var afterTile = isBlueprint
                                ? (ctx.BuildableData.GetTileAtLocalOffset(x, y) ?? ctx.Decision.WriteTile)
                                : ctx.Decision.WriteTile;
                            var afterMatrix = ctx.Decision.TransformMatrix;

                            BuildRecord? afterMeta = null;
                            if (afterTile != null && ctx.Decision.ConstructionId > 0)
                            {
                                Vector3Int anchor = isBlueprint ? bounds.position : cell;
                                BoundsInt fpBounds = isBlueprint ? bounds : new BoundsInt(cell, Vector3Int.one);
                                int maxHp = ctx.BuildableData != null ? ctx.BuildableData.maxHealth : 0;

                                afterMeta = new BuildRecord(
                                    afterTile,
                                    cell,
                                    BuildState.Completed,
                                    ctx.BuildableData,
                                    anchor,
                                    fpBounds,
                                    maxHp);
                            }

                            changeList.Add(new TileChange(cell, beforeTile, afterTile, beforeMeta, afterMeta, beforeMatrix, afterMatrix));
                        }
                    }
                }
            }

            if (changeList.Count == 0)
                return;

            var changes = changeList.ToArray();

            var instanceId = ctx.InstanceId;

            var command = new PlaceTilesCommand(
                targetTilemap,
                changes,
                _metadata,
                effectiveBounds,
                _emitRegionEvents ? (b) => NotifyRegionModified(instanceId, b) : (Action<BoundsInt>)null);

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
