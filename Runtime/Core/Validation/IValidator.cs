using TMBS.Core.Pipeline;

namespace TMBS.Core.Validation
{
    public interface IValidator
    {
        int Priority { get; }
        ValidationResult Validate(in PipelineContext ctx, ValidationMode mode);
    }
}

