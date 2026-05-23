using UnityEngine;
using UnityEngine.Tilemaps;
using TMBS.Core.Validation;

namespace TMBS.Unity.Preview
{
    public sealed class TilemapPreviewRenderer : IPreviewRenderer
    {
        private readonly Tilemap _tilemap;
        private TileBase _valid;
        private TileBase _invalid;

        private BoundsInt _last;
        private bool _hasLast;
        private TileBase _lastTile;

        private TileBase[] _buffer;     
        private TileBase[] _clearBuffer; 

        public TilemapPreviewRenderer(Tilemap tilemap, TileBase valid, TileBase invalid)
        {
            if (tilemap == null)
                throw new System.ArgumentNullException(nameof(tilemap));

            _tilemap = tilemap;
            _valid = valid;
            _invalid = invalid;
        }

        public void UpdateTiles(TileBase valid, TileBase invalid)
        {
            Hide();
            _valid   = valid;
            _invalid = invalid;
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

            int len = Volume(fullArea);
            EnsureBuffer(len);

            if (blockedMask == null || !blockedMask.AnyTrue())
            {
                FillArray(_buffer, len, _valid);
                _tilemap.SetTilesBlock(fullArea, _buffer);
                _last = fullArea;
                _hasLast = true;
                _lastTile = _valid;
                return;
            }

            bool sameBounds =
                blockedMask.Bounds.position == fullArea.position &&
                blockedMask.Bounds.size == fullArea.size;

            for (int i = 0; i < len; i++)
            {
                
                Vector3Int pos = CellAt(fullArea, i);
                bool isBlocked = sameBounds
                    ? blockedMask.Bits[i]
                    : (blockedMask.Contains(pos) && blockedMask.Bits[blockedMask.IndexOf(pos)]);

                _buffer[i] = isBlocked ? _invalid : _valid;
            }

            _tilemap.SetTilesBlock(fullArea, _buffer);
            _last = fullArea;
            _hasLast = true;
            _lastTile = null; 
        }

        public void Hide()
        {
            ClearPrevious();
            _lastTile = null;
        }

        private void ClearPrevious()
        {
            if (!_hasLast) return;
            
            int len = Volume(_last);
            EnsureClearBuffer(len);
            
            
            _tilemap.SetTilesBlock(_last, _clearBuffer);
            _hasLast = false;
        }

        private void UpdateIncremental(BoundsInt next, bool valid)
        {
            var tile = valid ? _valid : _invalid;

            if (_hasLast && SameBounds(_last, next) && _lastTile == tile)
                return;

            ClearPrevious();

            int len = Volume(next);
            EnsureBuffer(len);
            FillArray(_buffer, len, tile);
            
            _tilemap.SetTilesBlock(next, _buffer);
            _last = next;
            _hasLast = true;
            _lastTile = tile;
        }

        private void EnsureBuffer(int len)
        {
            if (_buffer == null || _buffer.Length != len)
                _buffer = new TileBase[len];
        }

        private void EnsureClearBuffer(int len)
        {
            if (_clearBuffer == null || _clearBuffer.Length != len)
            {
                _clearBuffer = new TileBase[len]; 
                return;
            }
            
            System.Array.Clear(_clearBuffer, 0, len);
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