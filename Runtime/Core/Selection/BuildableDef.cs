using System;
using UnityEngine.Tilemaps;
using TMBS.Runtime.Config;

namespace TMBS.Core.Selection
{
    [Serializable]
    public abstract class BuildableDef
    {
        /// <summary>
        /// The primary tile to place and to use for preview.
        /// </summary>
        public abstract TileBase ResolvePrimaryTile();

        /// <summary>
        /// True for single-tile placement. False for multi-tile blueprints.
        /// </summary>
        public abstract bool IsSingleTile { get; }

        /// <summary>
        /// Returns the associated TmbsBuildableData, or null if this
        /// definition was created from a raw TileBase without data.
        /// </summary>
        public virtual TmbsBuildableData ResolveData() => null;
    }
}
