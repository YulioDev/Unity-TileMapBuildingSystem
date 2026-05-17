using TMBS.Core.Pipeline;

namespace TMBS.Core.Execution
{
    public interface IExecutionRouter
    {
        ExecutionDecision Decide(in PipelineContext ctx);
    }
}

