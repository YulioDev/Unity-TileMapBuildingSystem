using TMBS.Core.Metadata;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.History
{
    public readonly struct TileChange
    {
        public readonly Vector3Int Cell;
        public readonly TileBase BeforeTile;
        public readonly TileBase AfterTile;
        public readonly BuildRecord? BeforeMeta;
        public readonly BuildRecord? AfterMeta;

        public TileChange(
            Vector3Int cell,
            TileBase beforeTile,
            TileBase afterTile,
            BuildRecord? beforeMeta,
            BuildRecord? afterMeta)
        {
            Cell = cell;
            BeforeTile = beforeTile;
            AfterTile = afterTile;
            BeforeMeta = beforeMeta;
            AfterMeta = afterMeta;
        }
    }
}