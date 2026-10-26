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
        public readonly Matrix4x4 BeforeMatrix;
        public readonly Matrix4x4 AfterMatrix;

        public TileChange(
            Vector3Int cell,
            TileBase beforeTile,
            TileBase afterTile,
            BuildRecord? beforeMeta,
            BuildRecord? afterMeta,
            Matrix4x4 beforeMatrix = default,
            Matrix4x4 afterMatrix = default)
        {
            Cell = cell;
            BeforeTile = beforeTile;
            AfterTile = afterTile;
            BeforeMeta = beforeMeta;
            AfterMeta = afterMeta;
            BeforeMatrix = beforeMatrix == default ? Matrix4x4.identity : beforeMatrix;
            AfterMatrix = afterMatrix == default ? Matrix4x4.identity : afterMatrix;
        }
    }
}