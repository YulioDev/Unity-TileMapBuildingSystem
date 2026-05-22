using TMBS.Core.Metadata;
using TMBS.Unity.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.History
{
    public sealed class PlaceTilesCommand : IImmediateCommand
    {
        private readonly ITilemapWriteStrategy _writer;
        private readonly IMetadataStore _metadata;
        private readonly Tilemap _tilemap;
        private readonly BoundsInt _bounds;

        private readonly TileBase[] _beforeTiles;
        private readonly TileBase[] _afterTiles;

        private readonly TileChange[] _changes;

        private readonly BuildRecord?[] _beforeMeta;
        private readonly BuildRecord?[] _afterMeta;

        private readonly bool _isSparse;
        private readonly System.Action<BoundsInt> _onRegionModified;

        public PlaceTilesCommand(
            ITilemapWriteStrategy writer,
            IMetadataStore metadata,
            Tilemap tilemap,
            BoundsInt bounds,
            TileBase[] beforeTiles,
            TileBase[] afterTiles,
            BuildRecord?[] beforeMeta,
            BuildRecord?[] afterMeta,
            TileChange[] changes,
            bool isSparse,
            System.Action<BoundsInt> onRegionModified = null)
        {
            _writer = writer;
            _metadata = metadata;
            _tilemap = tilemap;
            _bounds = bounds;
            _beforeTiles = beforeTiles;
            _afterTiles = afterTiles;
            _beforeMeta = beforeMeta;
            _afterMeta = afterMeta;
            _changes = changes;
            _isSparse = isSparse;
            _onRegionModified = onRegionModified;
        }

        public void Execute()
        {
            if (_isSparse)
                ApplySparse(true);
            else
                ApplyDense(true);

            _onRegionModified?.Invoke(_bounds);
        }

        public void Undo()
        {
            if (_isSparse)
                ApplySparse(false);
            else
                ApplyDense(false);

            _onRegionModified?.Invoke(_bounds);
        }

        public void Redo()
        {
            Execute();
        }

        private void ApplySparse(bool useAfter)
        {
            if (_changes == null) return;
            var writes = new TileCellWrite[_changes.Length];

            for (int i = 0; i < _changes.Length; i++)
            {
                var change = _changes[i];
                writes[i] = new TileCellWrite(change.Cell, useAfter ? change.AfterTile : change.BeforeTile);
                
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
        }

        private void ApplyDense(bool useAfter)
        {
            _writer.Write(_tilemap, _bounds, useAfter ? _afterTiles : _beforeTiles);

            if (_metadata == null || _beforeMeta == null) return;

            var metaArray = useAfter ? _afterMeta : _beforeMeta;
            
            for (int i = 0; i < metaArray.Length; i++)
            {
                var m = metaArray[i];
                Vector3Int cell = TMBS.Core.Grid.TileBlockIndex.CellAt(_bounds, i);

                if (!m.HasValue)
                    _metadata.Remove(cell);
                else
                    _metadata.Set(m.Value);
            }
        }
    }
}
