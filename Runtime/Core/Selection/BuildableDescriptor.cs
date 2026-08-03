using System;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Selection
{
    [Serializable]
    public abstract class BuildableDescriptor
    {
        /// <summary>
        /// The primary tile to place and to use for preview.
        /// </summary>
        public abstract TileBase ResolvePrimaryTile();

        /// <summary>
        /// True for single-tile placement. False for multi-tile blueprints.
        /// </summary>
        public abstract bool IsSingleTile { get; }
    }
}
