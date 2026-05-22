using System;

namespace TMBS.Core.Catalog
{
    [Obsolete("Sustituido por BuildSelectionData y TileSelectionState")]
    public interface IBuildableSelectionService
    {
        bool HasSelection { get; }
        int SelectedId { get; }
        event Action<int> SelectionChanged;
        void Select(int id);
        void Clear();
    }
}

