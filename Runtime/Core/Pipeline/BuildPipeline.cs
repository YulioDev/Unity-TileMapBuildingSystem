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
        private readonly List<IPipelineStep> _steps;
        private readonly bool _clampDragBounds;
        private Vector3Int _dragStartCell;
        private bool _hasDragStart;

        public BuildPipeline(IGridSpace gridSpace, ValidatorPipeline validatorPipeline, IExecutionRouter router, List<IPipelineStep> steps, bool clampDragBounds)
        {
            _gridSpace = gridSpace;
            _validatorPipeline = validatorPipeline;
            _router = router;
            _steps = steps;
            _clampDragBounds = clampDragBounds;
        }

        public void CancelActiveOperation()
        {
            _hasDragStart = false;
        }

        public PipelineContext Process(string instanceId, in BuildIntent intent)
        {
            var cell = _gridSpace.WorldToCell(intent.WorldPoint);
            var ctx = new PipelineContext(
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
                    ctx = ctx.WithDragBounds(bounds);
                }
            }
            else if (intent.Type == BuildIntentType.Cancel)
            {
                _hasDragStart = false;
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                ctx = _steps[i].Execute(in ctx, in intent);
            }

            if (_clampDragBounds && ctx.HasDragBounds)
            {
                ctx = ApplyBoundsClamp(ctx);
            }

            if (intent.Type == BuildIntentType.Confirm)
            {
                var full = _validatorPipeline.Validate(in ctx, ValidationMode.Full);
                ctx = ctx.WithFullValidation(full);
                ctx = ctx.WithDecision(_router.Decide(in ctx));
                _hasDragStart = false;
            }
            else
            {
                var quick = _validatorPipeline.Validate(in ctx, ValidationMode.Quick);
                ctx = ctx.WithQuickValidation(quick);
            }

            return ctx;
        }

        private PipelineContext ApplyBoundsClamp(PipelineContext ctx)
        {
            if (!ctx.HasDragBounds) return ctx;

            BoundsInt globalBounds = default;
            bool found = false;

            for (int i = 0; i < _validatorPipeline.InternalValidatorsCount; i++)
            {
                if (_validatorPipeline.TryGetBounds(i, out globalBounds))
                {
                    found = true;
                    break;
                }
            }

            if (!found) return ctx;

            var b = ctx.DragBounds;

            int minX = Mathf.Max(b.xMin, globalBounds.xMin);
            int minY = Mathf.Max(b.yMin, globalBounds.yMin);
            int minZ = Mathf.Max(b.zMin, globalBounds.zMin);
            int maxX = Mathf.Min(b.xMax, globalBounds.xMax);
            int maxY = Mathf.Min(b.yMax, globalBounds.yMax);
            int maxZ = Mathf.Min(b.zMax, globalBounds.zMax);

            if (maxX <= minX || maxY <= minY || maxZ <= minZ)
            {
                var clampedToCursor = new BoundsInt(ctx.Cell, new Vector3Int(1, 1, 1));
                return ctx.WithDragBounds(clampedToCursor);
            }

            var newBounds = new BoundsInt(
                new Vector3Int(minX, minY, minZ),
                new Vector3Int(maxX - minX, maxY - minY, maxZ - minZ)
            );

            return ctx.WithDragBounds(newBounds);
        }

        private static BoundsInt ComputeRectBounds(Vector3Int a, Vector3Int b)
        {
            int minX = a.x < b.x ? a.x : b.x;
            int minY = a.y < b.y ? a.y : b.y;
            int maxX = a.x > b.x ? a.x : b.x;
            int maxY = a.y > b.y ? a.y : b.y;

            var pos = new Vector3Int(minX, minY, 0);
            var size = new Vector3Int((maxX - minX) + 1, (maxY - minY) + 1, 1);
            return new BoundsInt(pos, size);
        }
    }
}

