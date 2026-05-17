using TMBS.Core.Pipeline;

namespace TMBS.Core.Execution
{
    public sealed class ImmediateOnlyRouter : IExecutionRouter
    {
        public ExecutionDecision Decide(in PipelineContext ctx)
        {
            return ctx.FullValidation.IsValid ? ExecutionDecision.ExecuteImmediate : ExecutionDecision.Reject;
        }
    }
}

