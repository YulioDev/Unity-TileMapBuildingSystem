using System;
using UnityEngine.Tilemaps;
using TMBS.Runtime.Config;

namespace TMBS.Core.Selection
{
    [Serializable]
    public abstract class BuildableDef
    {
        /// <summary>Primary tile used for placement and preview.</summary>
        public abstract TileBase ResolvePrimaryTile();

        /// <summary>Associated buildable data asset, or null if backed by a raw tile.</summary>
        public virtual TmbsBuildableData ResolveData() => null;
    }
}
