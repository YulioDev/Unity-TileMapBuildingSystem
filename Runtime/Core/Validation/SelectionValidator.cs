using System;
using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    [Serializable]
    public sealed class SelectionValidator : IValidator
    {
        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            if (!ctx.HasSelection && !ctx.AlternateBehaviour)
                return ValidationResult.Invalid(ValidationFailure.MissingSelection);
            return ValidationResult.Valid;
        }
    }
}

