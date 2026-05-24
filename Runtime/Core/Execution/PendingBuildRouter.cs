using TMBS.Core.Pipeline;

namespace TMBS.Core.Execution
{
    public sealed class PendingBuildRouter : IExecutionRouter
    {
        public ExecutionDecision Decide(in PipelineContext ctx)
        {
            if (!ctx.FullValidation.IsValid)
                return ExecutionDecision.Reject;

            return new ExecutionDecision(
                ExecutionDecisionType.CreatePending,
                ctx.FullValidation.WriteMask,
                ctx.SelectedTile,
                ctx.SelectedTile,
                1);
        }
    }
}