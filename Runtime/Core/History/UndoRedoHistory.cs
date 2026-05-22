using System.Collections.Generic;

namespace TMBS.Core.History
{
    public sealed class UndoRedoHistory : IUndoRedoHistory
    {
        private readonly List<IImmediateCommand> _undo = new List<IImmediateCommand>(128);
        private readonly List<IImmediateCommand> _redo = new List<IImmediateCommand>(128);

        private int _capacity = 256;

        public int Capacity
        {
            get => _capacity;
            set
            {
                _capacity = value < 0 ? 0 : value;
                Trim();
            }
        }

        public void Push(IImmediateCommand command)
        {
            command.Execute();
            _undo.Add(command);
            _redo.Clear();
            Trim();
        }

        public bool TryUndo()
        {
            int count = _undo.Count;
            if (count == 0) return false;
            var cmd = _undo[count - 1];
            _undo.RemoveAt(count - 1);
            cmd.Undo();
            _redo.Add(cmd);
            return true;
        }

        public bool TryRedo()
        {
            int count = _redo.Count;
            if (count == 0) return false;
            var cmd = _redo[count - 1];
            _redo.RemoveAt(count - 1);
            cmd.Redo();
            _undo.Add(cmd);
            Trim();
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        private void Trim()
        {
            int overflow = _undo.Count - Capacity;
            if (overflow <= 0) return;
            _undo.RemoveRange(0, overflow);
        }
    }
}

