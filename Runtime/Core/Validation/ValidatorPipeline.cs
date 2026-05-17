using System.Collections.Generic;
using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    public sealed class ValidatorPipeline
    {
        private readonly List<IValidator> _validators;

        public ValidatorPipeline(List<IValidator> validators)
        {
            _validators = validators ?? new List<IValidator>();
        }

        public ValidationResult Validate(in PipelineContext ctx, ValidationMode mode)
        {
            for (int i = 0; i < _validators.Count; i++)
            {
                var result = _validators[i].Validate(in ctx, mode);
                if (!result.IsValid) return result;
            }
            return ValidationResult.Valid;
        }
    }
}

