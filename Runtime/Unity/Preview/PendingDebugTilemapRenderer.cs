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

        public PendingDebugTilemapRenderer(Tilemap tilemap, TmbsTileArchetypeConfig archetype)
        {
            _tilemap = tilemap != null ? tilemap : throw new System.ArgumentNullException(nameof(tilemap));
            _archetype = archetype != null ? archetype : throw new System.ArgumentNullException(nameof(archetype));
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

            var tile = ResolveTile(state);
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

        private TileBase ResolveTile(PendingConstructionState state)
        {
            switch (state)
            {
                case PendingConstructionState.WaitingForResources:
                    return _archetype.ResolveWaitingResourcesTile();
                case PendingConstructionState.ReadyToBuild:
                    return _archetype.ResolveReadyTile();
                case PendingConstructionState.InProgress:
                    return _archetype.ResolveInProgressTile();
                case PendingConstructionState.Completed:
                    return null;
                case PendingConstructionState.Cancelled:
                    return _archetype.ResolveInvalidTile();
                default:
                    return _archetype.ResolvePendingTile();
            }
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