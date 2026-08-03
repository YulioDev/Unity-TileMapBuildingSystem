using TMBS.Core.Pending;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Preview
{
    public sealed class PendingDebugTilemapRenderer
    {
        private readonly Tilemap _tilemap;
        private readonly TmbsTileArchetypeConfig _archetype;
        private readonly bool _useOriginalTileFallback;

        public PendingDebugTilemapRenderer(Tilemap tilemap, TmbsTileArchetypeConfig archetype, bool useOriginalTileFallback = true)
        {
            _tilemap = tilemap != null ? tilemap : throw new System.ArgumentNullException(nameof(tilemap));
            _archetype = archetype != null ? archetype : throw new System.ArgumentNullException(nameof(archetype));
            _useOriginalTileFallback = useOriginalTileFallback;
        }
        
        public void Clear()
        {
            if (_tilemap != null)
            {
                _tilemap.ClearAllTiles();
            }
        }

        public void RenderCell(PendingConstructionCell cell, PendingConstructionState state)
        {
            if (_tilemap == null || _archetype == null)
                return;

            var tile = ResolveTile(state, cell);
            if (tile == null)
                return;

            _tilemap.SetTile(cell.Cell, tile);
            _tilemap.SetColor(cell.Cell, ResolveColor(state));
        }

        public void ClearCell(Vector3Int cell)
        {
            if (_tilemap == null)
                return;

            _tilemap.SetTile(cell, null);
        }

        private TileBase ResolveTile(PendingConstructionState state, PendingConstructionCell cell)
        {
            switch (state)
            {
                case PendingConstructionState.WaitingForResources:
                    return _archetype.ResolveWaitingResourcesTile() ?? FallbackTile(cell);
                case PendingConstructionState.ReadyToBuild:
                    return _archetype.ResolveReadyTile() ?? FallbackTile(cell);
                case PendingConstructionState.InProgress:
                    return _archetype.ResolveInProgressTile() ?? FallbackTile(cell);
                case PendingConstructionState.Completed:
                    return null;
                case PendingConstructionState.Cancelled:
                    return _archetype.ResolveInvalidTile() ?? FallbackTile(cell);
                default:
                    return _archetype.ResolvePendingTile() ?? FallbackTile(cell);
            }
        }

        private TileBase FallbackTile(PendingConstructionCell cell)
        {
            return _useOriginalTileFallback ? cell.Tile : null;
        }

        private Color ResolveColor(PendingConstructionState state)
        {
            switch (state)
            {
                case PendingConstructionState.WaitingForResources:
                    return _archetype.waitingResourcesColor;
                case PendingConstructionState.ReadyToBuild:
                    return _archetype.readyColor;
                case PendingConstructionState.InProgress:
                    return _archetype.inProgressColor;
                case PendingConstructionState.Completed:
                    return Color.clear;
                case PendingConstructionState.Cancelled:
                    return _archetype.invalidColor;
                default:
                    return _archetype.pendingColor;
            }
        }
    }
}