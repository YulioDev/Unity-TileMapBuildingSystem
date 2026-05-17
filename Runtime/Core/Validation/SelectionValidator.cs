using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    public sealed class SelectionValidator : IValidator
    {
        public int Priority => 5;

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            
            if (!ctx.HasSelection && !ctx.AlternateBehaviour)
            {
                return ValidationResult.Invalid(ValidationFailure.MissingSelection);
            }
            return ValidationResult.Valid;
        }
    }
}

