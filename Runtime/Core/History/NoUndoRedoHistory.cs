namespace TMBS.Core.History
{
    public sealed class NoUndoRedoHistory : IUndoRedoHistory
    {
        public int Capacity { get; set; }

        public void Push(IImmediateCommand command)
        {
            command?.Execute();
        }

        public bool TryUndo()
        {
            return false;
        }

        public bool TryRedo()
        {
            return false;
        }

        public void Clear()
        {
        }
    }
}
