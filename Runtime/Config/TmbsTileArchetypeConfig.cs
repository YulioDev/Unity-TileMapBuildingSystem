using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public sealed class TmbsTileArchetypeConfig
    {
        [Tooltip("Base tile used when no state-specific tile is provided.")]
        public TileBase baseTile;

        [Header("Optional State Tiles")]
        public TileBase pendingTile;
        public TileBase waitingResourcesTile;
        public TileBase readyTile;
        public TileBase inProgressTile;
        public TileBase invalidTile;

        [Header("State Colors")]
        public Color pendingColor = new Color(1f, 1f, 1f, 0.35f);
        public Color waitingResourcesColor = new Color(1f, 0.65f, 0.15f, 0.45f);
        public Color readyColor = new Color(0.25f, 1f, 0.25f, 0.5f);
        public Color inProgressColor = new Color(0.25f, 0.6f, 1f, 0.65f);
        public Color invalidColor = new Color(1f, 0.1f, 0.1f, 0.55f);

        public TileBase ResolvePendingTile() => pendingTile != null ? pendingTile : baseTile;
        public TileBase ResolveWaitingResourcesTile() => waitingResourcesTile != null ? waitingResourcesTile : baseTile;
        public TileBase ResolveReadyTile() => readyTile != null ? readyTile : baseTile;
        public TileBase ResolveInProgressTile() => inProgressTile != null ? inProgressTile : baseTile;
        public TileBase ResolveInvalidTile() => invalidTile != null ? invalidTile : baseTile;
    }
}