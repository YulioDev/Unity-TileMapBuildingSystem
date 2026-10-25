using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using TMBS.Runtime.Config;

namespace TMBS.Core.Selection
{
    [Serializable]
    public sealed class SingleTileBuildable : BuildableDef
    {
        [SerializeField] private TileBase tile;
        [SerializeField] private TmbsBuildableData data;

        public SingleTileBuildable() { }

        public SingleTileBuildable(TileBase tile)
        {
            this.tile = tile;
        }

        public SingleTileBuildable(TmbsBuildableData data)
        {
            this.data = data;
            this.tile = data != null ? data.tile : null;
        }

        public override TileBase ResolvePrimaryTile()
        {
            if (data != null && data.tile != null)
                return data.tile;
            return tile;
        }

        public override TmbsBuildableData ResolveData() => data;
        public override bool IsSingleTile => true;
    }
}
