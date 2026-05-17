using TMBS.Core.Pipeline;
using UnityEngine;
using System;

namespace TMBS.Core.Validation
{
    [Serializable]
    public sealed class BoundsValidator : IValidator
    {
        public BoundsInt allowedBounds = new BoundsInt(-50, -50, 0, 100, 100, 1);

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (ctx.HasDragBounds)
            {
                var r = ctx.DragBounds;
                if (r.xMin < allowedBounds.xMin || r.xMax > allowedBounds.xMax ||
                    r.yMin < allowedBounds.yMin || r.yMax > allowedBounds.yMax ||
                    r.zMin < allowedBounds.zMin || r.zMax > allowedBounds.zMax)
                    return ValidationResult.Invalid(ValidationFailure.OutOfBounds);

                return ValidationResult.Valid;
            }

            if (!allowedBounds.Contains(ctx.Cell))
                return ValidationResult.Invalid(ValidationFailure.OutOfBounds);

            return ValidationResult.Valid;
        }
    }
}

