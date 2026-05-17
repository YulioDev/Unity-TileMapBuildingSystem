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
        private readonly TileBase[] _before;
        private readonly TileBase[] _after;
        private readonly int _buildableId;

        public PlaceTilesCommand(ITilemapBatchWriter writer, IMetadataStore metadata, Tilemap tilemap, BoundsInt bounds, TileBase[] before, TileBase[] after, int buildableId)
        {
            _writer = writer;
            _metadata = metadata;
            _tilemap = tilemap;
            _bounds = bounds;
            _before = before;
            _after = after;
            _buildableId = buildableId;
        }

        public void Execute()
        {
            _writer.WriteBlock(_tilemap, _bounds, _after);
            WriteMetadata(_after, BuildState.Completed);
        }

        public void Undo()
        {
            _writer.WriteBlock(_tilemap, _bounds, _before);
            WriteMetadata(_before, BuildState.Completed);
        }

        public void Redo()
        {
            Execute();
        }

        private void WriteMetadata(TileBase[] tiles, BuildState state)
        {
            int width = _bounds.size.x;
            int height = _bounds.size.y;
            var origin = _bounds.position;

            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                int cy = origin.y + y;
                for (int x = 0; x < width; x++)
                {
                    var t = tiles[idx++];
                    var cell = new Vector3Int(origin.x + x, cy, 0);
                    if (t == null)
                    {
                        _metadata.Remove(cell);
                    }
                    else
                    {
                        _metadata.Set(new BuildRecord(_buildableId, cell, state));
                    }
                }
            }
        }
    }
}

