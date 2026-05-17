using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    public interface IValidator
    {
        ValidationResult Validate(in PipelineContext ctx, ValidationMode mode);
    }
}

