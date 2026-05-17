using TMBS.Core.Catalog;
using TMBS.Core.Intents;

namespace TMBS.Core.Pipeline.Steps
{
    public sealed class SelectionGateStep : IPipelineStep
    {
        private readonly IBuildableSelectionService _selection;

        public SelectionGateStep(IBuildableSelectionService selection)
        {
            _selection = selection;
        }

        public PipelineContext Execute(in PipelineContext ctx, in BuildIntent intent)
        {
            return ctx.WithSelection(_selection.HasSelection, _selection.SelectedId);
        }
    }
}

