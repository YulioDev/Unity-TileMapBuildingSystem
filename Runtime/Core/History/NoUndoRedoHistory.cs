namespace TMBS.Core.History
{
    public sealed class NoUndoRedoHistory : IUndoRedoHistory
    {
        private int _capacity;

        public int Capacity
        {
            get => _capacity;
            set => _capacity = value < 0 ? 0 : value;
        }

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
