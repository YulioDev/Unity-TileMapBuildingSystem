using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Selection
{
    [Serializable]
    public sealed class SingleTileBuildable : BuildableDescriptor
    {
        [SerializeField] private TileBase tile;

        public SingleTileBuildable() { }

        public SingleTileBuildable(TileBase tile)
        {
            this.tile = tile;
        }

        public override TileBase ResolvePrimaryTile() => tile;
        public override bool IsSingleTile => true;
    }
}
