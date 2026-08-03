using UnityEngine;
using UnityEngine.Tilemaps;
using TMBS.Core.Validation;

namespace TMBS.Unity.Preview
{
    public sealed class TilemapPreviewRenderer : IPreviewRenderer
    {
        private readonly Tilemap _tilemap;
        private readonly Color _validColor;
        private readonly Color _invalidColor;

        private TileBase _tile;
        private BoundsInt _last;
        private bool _hasLast;
        private bool _lastValid;

        private TileBase[] _buffer;

        public TilemapPreviewRenderer(Tilemap tilemap, Color validColor, Color invalidColor)
        {
            if (tilemap == null)
                throw new System.ArgumentNullException(nameof(tilemap));

            _tilemap = tilemap;
            _validColor = validColor;
            _invalidColor = invalidColor;
        }

        public void UpdateTile(TileBase tile)
        {
            Hide();
            _tile = tile;
        }

        public void ShowCell(Vector3Int cell, bool valid)
        {
            var bounds = new BoundsInt(cell, new Vector3Int(1, 1, 1));
            UpdateIncremental(bounds, valid);
        }

        public void ShowRect(BoundsInt bounds, bool valid)
        {
            UpdateIncremental(bounds, valid);
        }

        public void ShowRectMasked(BoundsInt fullArea, CellMask blockedMask)
        {
            ClearPrevious();

            if (_tile == null) return;

            int len = Volume(fullArea);
            EnsureBuffer(len);
            FillArray(_buffer, len, _tile);
            _tilemap.SetTilesBlock(fullArea, _buffer);

            // Apply per-cell color overlay
            if (blockedMask == null || !blockedMask.AnyTrue())
            {
                ApplyColorToArea(fullArea, _validColor);
            }
            else
            {
                bool sameBounds =
                    blockedMask.Bounds.position == fullArea.position &&
                    blockedMask.Bounds.size == fullArea.size;

                for (int i = 0; i < len; i++)
                {
                    Vector3Int pos = CellAt(fullArea, i);
                    bool isBlocked = sameBounds
                        ? blockedMask.Bits[i]
                        : (blockedMask.Contains(pos) && blockedMask.Bits[blockedMask.IndexOf(pos)]);

                    _tilemap.SetColor(pos, isBlocked ? _invalidColor : _validColor);
                }
            }

            _last = fullArea;
            _hasLast = true;
        }

        public void Hide()
        {
            _tilemap.ClearAllTiles();
            _hasLast = false;
        }

        private void ClearPrevious()
        {
            if (!_hasLast) return;
            _tilemap.ClearAllTiles();
            _hasLast = false;
        }

        private void UpdateIncremental(BoundsInt next, bool valid)
        {
            if (_tile == null) return;

            if (_hasLast && SameBounds(_last, next) && _lastValid == valid)
                return;

            ClearPrevious();

            int len = Volume(next);
            EnsureBuffer(len);
            FillArray(_buffer, len, _tile);

            _tilemap.SetTilesBlock(next, _buffer);
            ApplyColorToArea(next, valid ? _validColor : _invalidColor);

            _last = next;
            _hasLast = true;
            _lastValid = valid;
        }

        private void ApplyColorToArea(BoundsInt area, Color color)
        {
            foreach (var pos in area.allPositionsWithin)
            {
                _tilemap.SetColor(pos, color);
            }
        }

        private void EnsureBuffer(int len)
        {
            if (_buffer == null || _buffer.Length != len)
                _buffer = new TileBase[len];
        }

        private static void FillArray(TileBase[] arr, int len, TileBase tile)
        {
            for (int i = 0; i < len; i++)
                arr[i] = tile;
        }

        private static int Volume(BoundsInt b)
        {
            return TMBS.Core.Grid.TileBlockIndex.Volume(b);
        }

        private static Vector3Int CellAt(BoundsInt b, int index)
        {
            return TMBS.Core.Grid.TileBlockIndex.CellAt(b, index);
        }

        private static bool SameBounds(in BoundsInt a, in BoundsInt b)
        {
            return a.xMin == b.xMin && a.xMax == b.xMax &&
                   a.yMin == b.yMin && a.yMax == b.yMax &&
                   a.zMin == b.zMin && a.zMax == b.zMax;
        }
    }
}