using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    public sealed class BoundsValidator : IValidator
    {
        public int Priority => 10;
        private readonly UnityEngine.BoundsInt _allowed;

        public BoundsValidator(UnityEngine.BoundsInt allowedBounds)
        {
            _allowed = allowedBounds;
        }

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (ctx.HasDragBounds)
            {
                var r = ctx.DragBounds;
                if (r.xMin < _allowed.xMin || r.xMax > _allowed.xMax ||
                    r.yMin < _allowed.yMin || r.yMax > _allowed.yMax ||
                    r.zMin < _allowed.zMin || r.zMax > _allowed.zMax)
                    return ValidationResult.Invalid(ValidationFailure.OutOfBounds);

                return ValidationResult.Valid;
            }

            if (!_allowed.Contains(ctx.Cell))
                return ValidationResult.Invalid(ValidationFailure.OutOfBounds);

            return ValidationResult.Valid;
        }
    }
}

