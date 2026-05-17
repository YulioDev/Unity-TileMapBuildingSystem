using TMBS.Core.Intents;
using TMBS.Core.Pipeline;

namespace TMBS.Core.Modes
{
    public interface IBuildMode
    {
        string Id { get; }
        bool RequiresPreview { get; }
        bool SupportsDrag { get; }
        
        
        
        PipelineContext Interpret(in BuildIntent intent, in PipelineContext ctx);
    }
}

