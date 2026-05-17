using TMBS.Core.Intents;

namespace TMBS.Core.Pipeline
{
    public interface IBuildPipeline
    {
        PipelineContext Process(string instanceId, in BuildIntent intent);
    }
}

