using TMBS.Core.Pipeline;

namespace TMBS.Core.Execution
{
    public sealed class ImmediateOnlyRouter : IExecutionRouter
    {
        public ExecutionDecision Decide(in PipelineContext ctx)
        {
            if (!ctx.FullValidation.IsValid)
                return ExecutionDecision.Reject;

            var tile = ctx.SelectedTile;

            return new ExecutionDecision(
                ExecutionDecisionType.ExecuteImmediate,
                ctx.FullValidation.WriteMask,
                tile,
                tile, 
                1 
            );
        }
    }
}

