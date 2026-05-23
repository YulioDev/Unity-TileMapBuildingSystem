using TMBS.Core.Events;
using TMBS.Core.Execution;
using TMBS.Core.Focus;
using TMBS.Core.History;
using TMBS.Core.Input;
using TMBS.Core.Metadata;
using TMBS.Core.Modes;
using TMBS.Core.Pipeline;
using TMBS.Core.Preview;
using TMBS.Core.Selection;
using TMBS.Unity.Preview;

namespace TMBS.Runtime.Facade
{
    public sealed class TmbsRuntimeContext
    {
        public readonly IBuildInputAdapter Input;
        public readonly IBuildPipeline Pipeline;
        public readonly IEventBus Events;
        public readonly IInputFocusService Focus;
        public readonly IUndoRedoHistory History;
        public readonly IMetadataStore Metadata;
        public readonly IPreviewRenderer Preview;
        public readonly TileSelectionState SelectionState;
        public readonly ImmediateBuildExecutor Executor;
        public readonly PreviewPolicyEvaluator PreviewEvaluator;
        public readonly IBuildMode ActiveMode;

        public TmbsRuntimeContext(
            IBuildInputAdapter input,
            IBuildPipeline pipeline,
            IEventBus events,
            IInputFocusService focus,
            IUndoRedoHistory history,
            IMetadataStore metadata,
            IPreviewRenderer preview,
            TileSelectionState selectionState,
            ImmediateBuildExecutor executor,
            PreviewPolicyEvaluator previewEvaluator,
            IBuildMode activeMode)
        {
            Input = input;
            Pipeline = pipeline;
            Events = events;
            Focus = focus;
            History = history;
            Metadata = metadata;
            Preview = preview;
            SelectionState = selectionState;
            Executor = executor;
            PreviewEvaluator = previewEvaluator;
            ActiveMode = activeMode;
        }
    }
}