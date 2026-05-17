using TMBS.Core.Intents;
using TMBS.Core.Pipeline;

namespace TMBS.Core.Modes
{
    public sealed class ImmediateBuildMode : IBuildMode
    {
        public string Id => "ImmediateBuild";
        public bool RequiresPreview => false;
        public bool SupportsDrag => true;

        public PipelineContext Interpret(in BuildIntent intent, in PipelineContext ctx)
        {
            
            
            if (intent.AlternateBehaviour)
            {
                
                
                return ctx.WithSelection(false, -1);
            }

            return ctx;
        }
    }
}

