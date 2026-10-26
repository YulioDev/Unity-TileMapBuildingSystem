using TMBS.Core.Pending;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Preview
{
    public sealed class PendingDebugTilemapRenderer
    {
        private readonly Tilemap _tilemap;
        private readonly TmbsStateTileConfig _archetype;
        private readonly bool _fallbackToOriginal;

        public PendingDebugTilemapRenderer(Tilemap tilemap, TmbsStateTileConfig archetype, bool fallbackToOriginal = true)
        {
            _tilemap = tilemap != null ? tilemap : throw new System.ArgumentNullException(nameof(tilemap));
            _archetype = archetype != null ? archetype : throw new System.ArgumentNullException(nameof(archetype));
            _fallbackToOriginal = fallbackToOriginal;
        }
        
        public void Clear()
        {
            if (_tilemap != null)
            {
                _tilemap.ClearAllTiles();
            }
        }

        public void RenderCell(PendingCell cell, PendingState state)
        {
            if (_tilemap == null || _archetype == null)
                return;

            var tile = ResolveTile(state, cell);
            if (tile == null)
                return;

            _tilemap.SetTile(cell.Cell, tile);
            _tilemap.SetTileFlags(cell.Cell, TileFlags.None);
            _tilemap.SetTransformMatrix(cell.Cell, cell.TransformMatrix);
            _tilemap.SetColor(cell.Cell, ResolveColor(state));
        }

        public void ClearCell(Vector3Int cell)
        {
            if (_tilemap == null)
                return;

            _tilemap.SetTile(cell, null);
            _tilemap.SetTransformMatrix(cell, Matrix4x4.identity);
            _tilemap.SetColor(cell, Color.white);
        }

        private TileBase ResolveTile(PendingState state, PendingCell cell)
        {
            switch (state)
            {
                case PendingState.WaitingForResources:
                    return _archetype.ResolveWaitingResourcesTile() ?? FallbackTile(cell);
                case PendingState.ReadyToBuild:
                    return _archetype.ResolveReadyTile() ?? FallbackTile(cell);
                case PendingState.InProgress:
                    return _archetype.ResolveInProgressTile() ?? FallbackTile(cell);
                case PendingState.Completed:
                    return null;
                case PendingState.Cancelled:
                    return _archetype.ResolveInvalidTile() ?? FallbackTile(cell);
                default:
                    return _archetype.ResolvePendingTile() ?? FallbackTile(cell);
            }
        }

        private TileBase FallbackTile(PendingCell cell)
        {
            return _fallbackToOriginal ? cell.Tile : null;
        }

        private Color ResolveColor(PendingState state)
        {
            switch (state)
            {
                case PendingState.WaitingForResources:
                    return _archetype.waitingResourcesColor;
                case PendingState.ReadyToBuild:
                    return _archetype.readyColor;
                case PendingState.InProgress:
                    return _archetype.inProgressColor;
                case PendingState.Completed:
                    return Color.clear;
                case PendingState.Cancelled:
                    return _archetype.invalidColor;
                default:
                    return _archetype.pendingColor;
            }
        }
    }
}
