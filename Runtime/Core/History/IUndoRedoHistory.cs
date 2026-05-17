namespace TMBS.Core.History
{
    public interface IUndoRedoHistory
    {
        void Push(IImmediateCommand command);
        bool TryUndo();
        bool TryRedo();
        void Clear();
        int Capacity { get; set; }
    }
}

