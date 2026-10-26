using TMBS.Core.Pipeline;
using TMBS.Core.Validation;

namespace TMBS.Core.Execution
{
    public sealed class ImmediateOnlyRouter : IExecutionRouter
    {
        public ExecutionDecision Decide(in PipelineContext ctx)
        {
            if (!ctx.FullValidation.IsValid)
            {
                return ExecutionDecision.Reject;
            }

            var mask = ctx.FullValidation.WriteMask;

            if (mask != null && !mask.AnyTrue())
            {
                return ExecutionDecision.Reject;
            }

            return new ExecutionDecision(
                ExecutionResult.ImmediatePlace,
                ctx.SelectedTile,
                null,
                mask,
                1,
                ctx.TransformMatrix);
        }
    }
}
