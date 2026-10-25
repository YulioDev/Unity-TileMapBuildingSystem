using TMBS.Core.Events;
using TMBS.Core.Execution;
using TMBS.Core.Focus;
using TMBS.Core.History;
using TMBS.Core.Input;
using TMBS.Core.Metadata;
using TMBS.Core.Modes;
using TMBS.Core.Pending;
using TMBS.Core.Pipeline;
using TMBS.Core.Preview;
using TMBS.Core.Selection;
using TMBS.Unity.Preview;

namespace TMBS.Runtime.Interface
{
    public sealed class TmbsSession
    {
        public IBuildInputAdapter Input { get; }
        public IBuildPipeline Pipeline { get; }
        public IEventBus Events { get; }
        public IInputFocusService Focus { get; }
        public IUndoRedoHistory History { get; }
        public IMetadataStore Metadata { get; }
        public IPreviewRenderer Preview { get; }
        public TileSelectionState SelectionState { get; }
        public IBuildExecutor Executor { get; }
        public PreviewToggle PreviewEvaluator { get; }
        public IBuildMode ActiveMode { get; }
        public IPendingWorkApi PendingWorkApi { get; }
        public PendingDebugBuilder PendingDebugBuilder { get; }

        public TmbsSession(
            IBuildInputAdapter input,
            IBuildPipeline pipeline,
            IEventBus events,
            IInputFocusService focus,
            IUndoRedoHistory history,
            IMetadataStore metadata,
            IPreviewRenderer preview,
            TileSelectionState selectionState,
            IBuildExecutor executor,
            PreviewToggle previewEvaluator,
            IBuildMode activeMode,
            IPendingWorkApi pendingWorkApi,
            PendingDebugBuilder pendingDebugBuilder)
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
            PendingWorkApi = pendingWorkApi;
            PendingDebugBuilder = pendingDebugBuilder;
        }
    }
}
