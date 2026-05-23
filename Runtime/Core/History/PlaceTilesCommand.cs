using System;
using TMBS.Core.Metadata;
using TMBS.Unity.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.History
{
    public sealed class PlaceTilesCommand : IImmediateCommand
    {
        private readonly Tilemap _tilemap;
        private readonly TileChange[] _changes;
        private readonly IMetadataStore _metadata;
        private readonly BoundsInt _bounds;
        private readonly Action<BoundsInt> _onRegionModified;

        public PlaceTilesCommand(
            Tilemap tilemap,
            TileChange[] changes,
            IMetadataStore metadata,
            BoundsInt bounds,
            Action<BoundsInt> onRegionModified)
        {
            _tilemap = tilemap;
            _changes = changes;
            _metadata = metadata;
            _bounds = bounds;
            _onRegionModified = onRegionModified;
        }

        public void Execute() => Apply(true);
        public void Undo() => Apply(false);
        public void Redo() => Apply(true);

        private void Apply(bool useAfter)
        {
            if (_changes == null || _changes.Length == 0)
                return;

            if (_changes == null || _changes.Length == 0)
                return;

            // Decide if this is dense or sparse based on bounds and change count
            int totalCells = TMBS.Core.Grid.TileBlockIndex.Volume(_bounds);
            if (_changes.Length >= totalCells)
            {
                // Dense path: build tiles array and call SetTilesBlock
                var tiles = new TileBase[totalCells];
                for (int i = 0; i < totalCells; i++)
                {
                    // Default to before tile
                    tiles[i] = null;
                }

                for (int i = 0; i < _changes.Length; i++)
                {
                    var change = _changes[i];
                    int index = TMBS.Core.Grid.TileBlockIndex.IndexOf(_bounds, change.Cell);
                    tiles[index] = useAfter ? change.AfterTile : change.BeforeTile;

                    if (_metadata != null)
                    {
                        var meta = useAfter ? change.AfterMeta : change.BeforeMeta;
                        if (!meta.HasValue)
                            _metadata.Remove(change.Cell);
                        else
                            _metadata.Set(meta.Value);
                    }
                }

                _tilemap.SetTilesBlock(_bounds, tiles);
                _onRegionModified?.Invoke(_bounds);
                return;
            }

            // Sparse path: use SetTiles with explicit positions
            var positions = new Vector3Int[_changes.Length];
            var tilesSparse = new TileBase[_changes.Length];

            for (int i = 0; i < _changes.Length; i++)
            {
                var change = _changes[i];
                positions[i] = change.Cell;
                tilesSparse[i] = useAfter ? change.AfterTile : change.BeforeTile;

                if (_metadata != null)
                {
                    var meta = useAfter ? change.AfterMeta : change.BeforeMeta;
                    if (!meta.HasValue)
                        _metadata.Remove(change.Cell);
                    else
                        _metadata.Set(meta.Value);
                }
            }

            _tilemap.SetTiles(positions, tilesSparse);
            _onRegionModified?.Invoke(_bounds);
        }
    }
}