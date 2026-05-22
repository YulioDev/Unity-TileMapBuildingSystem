using TMBS.Core.Intents;
using TMBS.Core.Selection;

namespace TMBS.Core.Pipeline.Steps
{
    public sealed class TileInjectionStep : IPipelineStep
    {
        private readonly TileSelectionState _state;

        public TileInjectionStep(TileSelectionState state)
        {
            _state = state;
        }

        public PipelineContext Execute(in PipelineContext ctx, in BuildIntent intent)
        {
            if (intent.AlternateBehaviour) return ctx;
            return ctx.WithTile(_state.CurrentTile);
        }
    }
}