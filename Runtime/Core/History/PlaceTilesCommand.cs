using System;
using TMBS.Core.Metadata;
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
            if (_tilemap == null)
            {
                Debug.LogError("TMBS: Cannot apply tile command. Tilemap is null.");
                return;
            }

            if (_changes == null || _changes.Length == 0)
                return;

            int totalCells = TMBS.Core.Grid.TileBlockIndex.Volume(_bounds);

            if (!ValidateChangesInsideBounds())
                return;

            if (_changes.Length == totalCells)
            {
                ApplyBatch(useAfter, totalCells);
                return;
            }

            ApplySparse(useAfter);
        }

        private bool ValidateChangesInsideBounds()
        {
            for (int i = 0; i < _changes.Length; i++)
            {
                if (!_bounds.Contains(_changes[i].Cell))
                {
                    Debug.LogError("TMBS: TileChange cell is outside command bounds. Aborting command to prevent corrupted writes.");
                    return false;
                }
            }

            return true;
        }

        private void ApplyBatch(bool useAfter, int totalCells)
        {
            var tiles = new TileBase[totalCells];

            for (int i = 0; i < _changes.Length; i++)
            {
                var change = _changes[i];
                int index = TMBS.Core.Grid.TileBlockIndex.IndexOf(_bounds, change.Cell);
                tiles[index] = useAfter ? change.AfterTile : change.BeforeTile;
            }

            _tilemap.SetTilesBlock(_bounds, tiles);

            ApplyMetadata(useAfter);
            _onRegionModified?.Invoke(_bounds);
        }

        private void ApplySparse(bool useAfter)
        {
            var positions = new Vector3Int[_changes.Length];
            var tilesSparse = new TileBase[_changes.Length];

            for (int i = 0; i < _changes.Length; i++)
            {
                var change = _changes[i];
                positions[i] = change.Cell;
                tilesSparse[i] = useAfter ? change.AfterTile : change.BeforeTile;
            }

            _tilemap.SetTiles(positions, tilesSparse);

            ApplyMetadata(useAfter);
            _onRegionModified?.Invoke(_bounds);
        }

        private void ApplyMetadata(bool useAfter)
        {
            if (_metadata == null)
                return;

            for (int i = 0; i < _changes.Length; i++)
            {
                var change = _changes[i];
                var meta = useAfter ? change.AfterMeta : change.BeforeMeta;

                if (!meta.HasValue)
                    _metadata.Remove(change.Cell);
                else
                    _metadata.Set(meta.Value);
            }
        }
    }
}