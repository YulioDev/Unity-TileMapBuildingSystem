using System;

namespace TMBS.Core.Catalog
{
    [Obsolete("Sustituido por BuildSelectionData y TileSelectionState")]
    public sealed class DefaultBuildableSelectionService : IBuildableSelectionService
    {
        public bool HasSelection { get; private set; }
        public int SelectedId { get; private set; }

        public event Action<int> SelectionChanged;

        public void Select(int id)
        {
            SelectedId = id;
            HasSelection = true;
            SelectionChanged?.Invoke(id);
        }

        public void Clear()
        {
            HasSelection = false;
            SelectedId = -1;
            SelectionChanged?.Invoke(-1);
        }
    }
}

