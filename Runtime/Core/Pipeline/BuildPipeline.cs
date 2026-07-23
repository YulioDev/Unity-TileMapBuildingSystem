using System.Collections.Generic;
using TMBS.Core.Execution;
using TMBS.Core.Grid;
using TMBS.Core.Intents;
using TMBS.Core.Validation;
using UnityEngine;

namespace TMBS.Core.Pipeline
{
    public sealed class BuildPipeline : IBuildPipeline
    {
        private readonly IGridSpace _gridSpace;
        private readonly ValidatorPipeline _validatorPipeline;
        private readonly IExecutionRouter _router;
        private readonly TMBS.Core.Selection.TileSelectionState _selectionState;
        private readonly TMBS.Core.Modes.IBuildMode _activeMode;
        private readonly bool _clampDragBounds;
        private readonly BoundsInt? _globalBounds;
        private Vector3Int _dragStartCell;
        private bool _hasDragStart;
        private readonly PipelineContext _ctx = new PipelineContext();

        public BuildPipeline(
            IGridSpace gridSpace, 
            ValidatorPipeline validatorPipeline, 
            IExecutionRouter router, 
            TMBS.Core.Selection.TileSelectionState selectionState, 
            TMBS.Core.Modes.IBuildMode activeMode, 
            bool clampDragBounds,
            BoundsInt? globalBounds)
        {
            _gridSpace = gridSpace;
            _validatorPipeline = validatorPipeline;
            _router = router;
            _selectionState = selectionState;
            _activeMode = activeMode;
            _clampDragBounds = clampDragBounds;
            _globalBounds = globalBounds;
        }

        public void CancelActiveOperation()
        {
            _hasDragStart = false;
        }

        public PipelineContext Process(string instanceId, in BuildIntent intent)
        {
            var cell = _gridSpace.WorldToCell(intent.WorldPoint);
            
            _ctx.Reset(
                instanceId,
                intent.WorldPoint,
                cell,
                true,
                intent.AlternateBehaviour,
                null,
                ValidationResult.Valid,
                ValidationResult.Valid,
                ExecutionDecision.Reject,
                default,
                false,
                default);

            if (intent.Type == BuildIntentType.DragStart)
            {
                _dragStartCell = cell;
                _hasDragStart = true;
            }
            else if (intent.Type == BuildIntentType.DragUpdate || intent.Type == BuildIntentType.DragEnd || intent.Type == BuildIntentType.Confirm)
            {
                if (_hasDragStart)
                {
                    var bounds = ComputeRectBounds(_dragStartCell, cell);
                    _ctx.DragBounds = bounds;
                    _ctx.HasDragBounds = true;
                }
            }
            else if (intent.Type == BuildIntentType.Cancel)
            {
                _hasDragStart = false;
            }

            if (!intent.AlternateBehaviour && _selectionState != null)
            {
                _ctx.SelectedTile = _selectionState.CurrentTile;
            }

            if (_activeMode != null)
            {
                var interpreted = _activeMode.Interpret(in intent, _ctx);
            }

            if (_clampDragBounds && _ctx.HasDragBounds)
            {
                ApplyBoundsClamp();
            }

            if (intent.Type == BuildIntentType.Confirm)
            {
                var full = _validatorPipeline.Validate(_ctx, ValidationMode.Full);
                _ctx.FullValidation = full;
                _ctx.Feedback = full.Feedback;
                _ctx.Decision = _router.Decide(_ctx);
                _hasDragStart = false;
            }
            else
            {
                var quick = _validatorPipeline.Validate(_ctx, ValidationMode.Quick);
                _ctx.QuickValidation = quick;
                _ctx.Feedback = quick.Feedback;
            }

            return _ctx;
        }

        private void ApplyBoundsClamp()
        {
            if (!_ctx.HasDragBounds || !_globalBounds.HasValue) return;

            var globalBounds = _globalBounds.Value;
            var b = _ctx.DragBounds;

            int minX = Mathf.Max(b.xMin, globalBounds.xMin);
            int minY = Mathf.Max(b.yMin, globalBounds.yMin);
            int minZ = Mathf.Max(b.zMin, globalBounds.zMin);
            int maxX = Mathf.Min(b.xMax, globalBounds.xMax);
            int maxY = Mathf.Min(b.yMax, globalBounds.yMax);
            int maxZ = Mathf.Min(b.zMax, globalBounds.zMax);

            if (maxX <= minX || maxY <= minY || maxZ <= minZ)
            {
                var clampedToCursor = new BoundsInt(_ctx.Cell, new Vector3Int(1, 1, 1));
                _ctx.DragBounds = clampedToCursor;
                return;
            }

            var newBounds = new BoundsInt(
                new Vector3Int(minX, minY, minZ),
                new Vector3Int(maxX - minX, maxY - minY, maxZ - minZ)
            );

            _ctx.DragBounds = newBounds;
        }

        private static BoundsInt ComputeRectBounds(Vector3Int a, Vector3Int b)
        {
            int minX = a.x < b.x ? a.x : b.x;
            int minY = a.y < b.y ? a.y : b.y;
            int maxX = a.x > b.x ? a.x : b.x;
            int maxY = a.y > b.y ? a.y : b.y;

            var pos = new Vector3Int(minX, minY, a.z);
            var size = new Vector3Int((maxX - minX) + 1, (maxY - minY) + 1, 1);
            return new BoundsInt(pos, size);
        }
    }
}

