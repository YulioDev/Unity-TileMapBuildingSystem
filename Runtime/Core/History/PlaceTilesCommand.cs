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
        private readonly TilemapBatchWriter _writer;
        private readonly TileChange[] _changes;
        private readonly IMetadataStore _metadata;
        private readonly BoundsInt _bounds;
        private readonly Action<BoundsInt> _onRegionModified;

        public PlaceTilesCommand(
            Tilemap tilemap,
            TilemapBatchWriter writer,
            TileChange[] changes,
            IMetadataStore metadata,
            BoundsInt bounds,
            Action<BoundsInt> onRegionModified)
        {
            _tilemap = tilemap;
            _writer = writer;
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

            var writes = new TileCellWrite[_changes.Length];

            for (int i = 0; i < _changes.Length; i++)
            {
                var change = _changes[i];

                writes[i] = new TileCellWrite(
                    change.Cell,
                    useAfter ? change.AfterTile : change.BeforeTile);

                if (_metadata != null)
                {
                    var meta = useAfter ? change.AfterMeta : change.BeforeMeta;

                    if (!meta.HasValue)
                        _metadata.Remove(change.Cell);
                    else
                        _metadata.Set(meta.Value);
                }
            }

            _writer.Write(_tilemap, writes);
            _onRegionModified?.Invoke(_bounds);
        }
    }
}