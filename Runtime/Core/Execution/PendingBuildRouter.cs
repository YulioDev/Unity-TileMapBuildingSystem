using TMBS.Core.Pipeline;

namespace TMBS.Core.Execution
{
    public sealed class PendingBuildRouter : IExecutionRouter
    {
        public ExecutionDecision Decide(in PipelineContext ctx) =>
            ExecutionDecision.FromValidation(in ctx, ExecutionResult.PendingOrder);
    }
}
