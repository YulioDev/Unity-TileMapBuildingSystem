using TMBS.Core.Metadata;
using TMBS.Unity.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.History
{
    public sealed class PlaceTilesCommand : IImmediateCommand
    {
        private readonly ITilemapBatchWriter _writer;
        private readonly IMetadataStore _metadata;
        private readonly Tilemap _tilemap;
        private readonly BoundsInt _bounds;

        private readonly TileBase[] _beforeTiles;
        private readonly TileBase[] _afterTiles;

        private readonly BuildRecord?[] _beforeMeta;
        private readonly BuildRecord?[] _afterMeta;

        public PlaceTilesCommand(
            ITilemapBatchWriter writer,
            IMetadataStore metadata,
            Tilemap tilemap,
            BoundsInt bounds,
            TileBase[] beforeTiles,
            TileBase[] afterTiles,
            BuildRecord?[] beforeMeta,
            BuildRecord?[] afterMeta)
        {
            _writer = writer;
            _metadata = metadata;
            _tilemap = tilemap;
            _bounds = bounds;
            _beforeTiles = beforeTiles;
            _afterTiles = afterTiles;
            _beforeMeta = beforeMeta;
            _afterMeta = afterMeta;
        }

        public void Execute()
        {
            _writer.WriteBlock(_tilemap, _bounds, _afterTiles);
            ApplyMetadata(_afterMeta);
        }

        public void Undo()
        {
            _writer.WriteBlock(_tilemap, _bounds, _beforeTiles);
            ApplyMetadata(_beforeMeta);
        }

        public void Redo()
        {
            Execute();
        }

        private void ApplyMetadata(BuildRecord?[] meta)
        {
            if (_metadata == null) return;
            if (meta == null) return;

            for (int i = 0; i < meta.Length; i++)
            {
                var m = meta[i];
                Vector3Int cell = m.HasValue
                    ? m.Value.Cell
                    : new Vector3Int(
                        _bounds.position.x + (i % _bounds.size.x),
                        _bounds.position.y + ((i / _bounds.size.x) % _bounds.size.y),
                        _bounds.position.z + (i / (_bounds.size.x * _bounds.size.y))
                      );

                if (!m.HasValue)
                    _metadata.Remove(cell);
                else
                    _metadata.Set(m.Value);
            }
        }
    }
}