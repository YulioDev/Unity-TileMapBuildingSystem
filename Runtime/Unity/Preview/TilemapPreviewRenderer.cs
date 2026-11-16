using UnityEngine;
using UnityEngine.Tilemaps;
using TMBS.Core.Validation;

namespace TMBS.Unity.Preview
{
    public sealed class TilemapPreviewRenderer : IPreviewRenderer
    {
        private static Tile _fallbackIndicatorTile;

        private readonly Tilemap _tilemap;
        private readonly Color _validColor;
        private readonly Color _invalidColor;

        private TileBase _tile;
        private TileBase _lastRenderedTile;
        private BoundsInt _last;
        private bool _hasLast;
        private bool _lastValid;
        private Matrix4x4 _transformMatrix = Matrix4x4.identity;
        private Matrix4x4 _lastMatrix = Matrix4x4.identity;

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

        public void UpdateTransform(Matrix4x4 matrix)
        {
            _transformMatrix = matrix;
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

            TileBase activeTile = _tile != null ? _tile : GetFallbackIndicatorTile();

            int len = Volume(fullArea);
            EnsureBuffer(len);
            FillArray(_buffer, len, activeTile);
            _tilemap.SetTilesBlock(fullArea, _buffer);

            // Apply per-cell transform and color overlay
            bool sameBounds = blockedMask != null &&
                blockedMask.Bounds.position == fullArea.position &&
                blockedMask.Bounds.size == fullArea.size;

            for (int i = 0; i < len; i++)
            {
                Vector3Int pos = CellAt(fullArea, i);
                _tilemap.SetTileFlags(pos, TileFlags.None);
                _tilemap.SetTransformMatrix(pos, _transformMatrix);

                bool isBlocked = blockedMask != null && (sameBounds
                    ? blockedMask.Bits[i]
                    : (blockedMask.Contains(pos) && blockedMask.Bits[blockedMask.IndexOf(pos)]));

                _tilemap.SetColor(pos, isBlocked ? _invalidColor : _validColor);
            }

            _last = fullArea;
            _hasLast = true;
            _lastMatrix = _transformMatrix;
            _lastRenderedTile = activeTile;
        }

        public void Hide()
        {
            _tilemap.ClearAllTiles();
            _hasLast = false;
            _lastRenderedTile = null;
        }

        private void ClearPrevious()
        {
            if (!_hasLast) return;
            _tilemap.ClearAllTiles();
            _hasLast = false;
            _lastRenderedTile = null;
        }

        private void UpdateIncremental(BoundsInt next, bool valid)
        {
            TileBase activeTile = _tile != null ? _tile : GetFallbackIndicatorTile();

            if (_hasLast &&
                SameBounds(_last, next) &&
                _lastValid == valid &&
                _lastMatrix == _transformMatrix &&
                _lastRenderedTile == activeTile)
                return;

            ClearPrevious();

            int len = Volume(next);
            EnsureBuffer(len);
            FillArray(_buffer, len, activeTile);

            _tilemap.SetTilesBlock(next, _buffer);
            ApplyTransformAndColorToArea(next, valid ? _validColor : _invalidColor);

            _last = next;
            _hasLast = true;
            _lastValid = valid;
            _lastMatrix = _transformMatrix;
            _lastRenderedTile = activeTile;
        }

        internal static Tile GetFallbackIndicatorTile()
        {
            if (_fallbackIndicatorTile != null)
                return _fallbackIndicatorTile;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.HideAndDontSave;

            _fallbackIndicatorTile = ScriptableObject.CreateInstance<Tile>();
            _fallbackIndicatorTile.sprite = sprite;
            _fallbackIndicatorTile.color = Color.white;
            _fallbackIndicatorTile.hideFlags = HideFlags.HideAndDontSave;

            return _fallbackIndicatorTile;
        }

        private void ApplyTransformAndColorToArea(BoundsInt area, Color color)
        {
            foreach (var pos in area.allPositionsWithin)
            {
                _tilemap.SetTileFlags(pos, TileFlags.None);
                _tilemap.SetTransformMatrix(pos, _transformMatrix);
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