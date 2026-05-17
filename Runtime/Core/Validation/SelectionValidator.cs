using System;
using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    [Serializable]
    public sealed class SelectionValidator : IValidator
    {
        public bool clampPreviewIfInvalid = true;

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (!ctx.HasSelection && !ctx.AlternateBehaviour)
            {
                if (clampPreviewIfInvalid && ctx.HasDragBounds)
                {
                    var opBounds = ValidationUtil.GetOperationBounds(in ctx);
                    return ValidationResult.InvalidWith(
                        ValidationFailure.MissingSelection, 
                        new ValidationFeedback(CellMask.AllTrue(opBounds))
                    );
                }

                return ValidationResult.Invalid(ValidationFailure.MissingSelection);
            }

            return ValidationResult.Valid;
        }
    }
}