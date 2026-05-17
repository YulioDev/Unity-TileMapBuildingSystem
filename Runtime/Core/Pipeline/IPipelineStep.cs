using TMBS.Core.Intents;

namespace TMBS.Core.Pipeline
{
    public interface IPipelineStep
    {
        PipelineContext Execute(in PipelineContext ctx, in BuildIntent intent);
    }
}

