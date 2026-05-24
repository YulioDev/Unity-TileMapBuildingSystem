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
                TrimUndo();
                TrimRedo();
            }
        }

        public void Push(IImmediateCommand command)
        {
            if (command == null) return;
            command.Execute();
            _undo.Add(command);
            _redo.Clear();
            TrimUndo();
        }

        public bool TryUndo()
        {
            int count = _undo.Count;
            if (count == 0) return false;
            var cmd = _undo[count - 1];
            cmd.Undo();
            _undo.RemoveAt(count - 1);
            _redo.Add(cmd);
            TrimRedo();
            return true;
        }

        public bool TryRedo()
        {
            int count = _redo.Count;
            if (count == 0) return false;
            var cmd = _redo[count - 1];
            cmd.Redo();
            _redo.RemoveAt(count - 1);
            _undo.Add(cmd);
            TrimUndo();
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        private void TrimUndo() => Trim(_undo);
        private void TrimRedo() => Trim(_redo);

        private void Trim(List<IImmediateCommand> list)
        {
            int overflow = list.Count - Capacity;
            if (overflow <= 0) return;
            list.RemoveRange(0, overflow);
        }
    }
}

