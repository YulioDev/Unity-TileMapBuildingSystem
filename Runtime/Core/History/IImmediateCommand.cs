namespace TMBS.Core.History
{
    public interface IImmediateCommand
    {
        void Execute();
        void Undo();
        void Redo();
    }
}

