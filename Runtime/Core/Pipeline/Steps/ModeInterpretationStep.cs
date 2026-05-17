using TMBS.Core.Intents;
using TMBS.Core.Modes;

namespace TMBS.Core.Pipeline.Steps
{
    public sealed class ModeInterpretationStep : IPipelineStep
    {
        private readonly IBuildMode _activeMode;

        public ModeInterpretationStep(IBuildMode activeMode)
        {
            _activeMode = activeMode;
        }

        public PipelineContext Execute(in PipelineContext ctx, in BuildIntent intent)
        {
            if (_activeMode == null) return ctx;
            return _activeMode.Interpret(in intent, in ctx);
        }
    }
}

