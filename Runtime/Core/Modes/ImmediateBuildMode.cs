using TMBS.Core.Intents;
using TMBS.Core.Pipeline;

namespace TMBS.Core.Modes
{
    public sealed class ImmediateBuildMode : IBuildMode
    {
        public string Id => "ImmediateBuild";
        public bool RequiresPreview => false;

        public PipelineContext Interpret(in BuildIntent intent, in PipelineContext ctx)
        {
            if (intent.AlternateBehaviour)
            {
                ctx.SelectedTile = null;
            }

            return ctx;
        }
    }
}

