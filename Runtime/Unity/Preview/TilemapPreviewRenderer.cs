using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Preview
{
    public sealed class TilemapPreviewRenderer : IPreviewRenderer
    {
        private readonly Tilemap _tilemap;
        private readonly TileBase _valid;
        private readonly TileBase _invalid;

        private BoundsInt _last;
        private bool _hasLast;
        private TileBase _lastTile;

        public TilemapPreviewRenderer(Tilemap tilemap, TileBase valid, TileBase invalid)
        {
            _tilemap = tilemap;
            _valid = valid;
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

        public void Hide()
        {
            if (!_hasLast) return;
            ClearRect(_last, _last.xMin, _last.xMax, _last.yMin, _last.yMax, _last.zMin, _last.zMax);
            _hasLast = false;
            _lastTile = null;
        }

        private void UpdateIncremental(BoundsInt next, bool valid)
        {
            var tile = valid ? _valid : _invalid;

            if (!_hasLast)
            {
                FillRect(next, tile, next.xMin, next.xMax, next.yMin, next.yMax, next.zMin, next.zMax);
                _last = next;
                _hasLast = true;
                _lastTile = tile;
                return;
            }

            if (_lastTile != tile)
            {
                ClearDifference(_last, next);
                FillRect(next, tile, next.xMin, next.xMax, next.yMin, next.yMax, next.zMin, next.zMax);
                _last = next;
                _lastTile = tile;
                return;
            }

            if (SameBounds(_last, next)) return;

            var ixMin = _last.xMin > next.xMin ? _last.xMin : next.xMin;
            var iyMin = _last.yMin > next.yMin ? _last.yMin : next.yMin;
            var izMin = _last.zMin > next.zMin ? _last.zMin : next.zMin;

            var ixMax = _last.xMax < next.xMax ? _last.xMax : next.xMax;
            var iyMax = _last.yMax < next.yMax ? _last.yMax : next.yMax;
            var izMax = _last.zMax < next.zMax ? _last.zMax : next.zMax;

            if (ixMin >= ixMax || iyMin >= iyMax || izMin >= izMax)
            {
                ClearRect(_last, _last.xMin, _last.xMax, _last.yMin, _last.yMax, _last.zMin, _last.zMax);
                FillRect(next, tile, next.xMin, next.xMax, next.yMin, next.yMax, next.zMin, next.zMax);
                _last = next;
                return;
            }

            ClearStripsOutsideIntersection(_last, ixMin, ixMax, iyMin, iyMax, izMin, izMax);
            FillStripsOutsideIntersection(next, tile, ixMin, ixMax, iyMin, iyMax, izMin, izMax);

            _last = next;
        }

        private void ClearDifference(BoundsInt oldB, BoundsInt newB)
        {
            var ixMin = oldB.xMin > newB.xMin ? oldB.xMin : newB.xMin;
            var iyMin = oldB.yMin > newB.yMin ? oldB.yMin : newB.yMin;
            var izMin = oldB.zMin > newB.zMin ? oldB.zMin : newB.zMin;

            var ixMax = oldB.xMax < newB.xMax ? oldB.xMax : newB.xMax;
            var iyMax = oldB.yMax < newB.yMax ? oldB.yMax : newB.yMax;
            var izMax = oldB.zMax < newB.zMax ? oldB.zMax : newB.zMax;

            if (ixMin >= ixMax || iyMin >= iyMax || izMin >= izMax)
            {
                ClearRect(oldB, oldB.xMin, oldB.xMax, oldB.yMin, oldB.yMax, oldB.zMin, oldB.zMax);
                return;
            }

            ClearStripsOutsideIntersection(oldB, ixMin, ixMax, iyMin, iyMax, izMin, izMax);
        }

        private void ClearStripsOutsideIntersection(BoundsInt b, int ixMin, int ixMax, int iyMin, int iyMax, int izMin, int izMax)
        {
            if (b.xMin < ixMin) ClearRect(b, b.xMin, ixMin, b.yMin, b.yMax, b.zMin, b.zMax);
            if (ixMax < b.xMax) ClearRect(b, ixMax, b.xMax, b.yMin, b.yMax, b.zMin, b.zMax);

            if (b.yMin < iyMin) ClearRect(b, ixMin, ixMax, b.yMin, iyMin, b.zMin, b.zMax);
            if (iyMax < b.yMax) ClearRect(b, ixMin, ixMax, iyMax, b.yMax, b.zMin, b.zMax);

            if (b.zMin < izMin) ClearRect(b, ixMin, ixMax, iyMin, iyMax, b.zMin, izMin);
            if (izMax < b.zMax) ClearRect(b, ixMin, ixMax, iyMin, iyMax, izMax, b.zMax);
        }

        private void FillStripsOutsideIntersection(BoundsInt b, TileBase tile, int ixMin, int ixMax, int iyMin, int iyMax, int izMin, int izMax)
        {
            if (b.xMin < ixMin) FillRect(b, tile, b.xMin, ixMin, b.yMin, b.yMax, b.zMin, b.zMax);
            if (ixMax < b.xMax) FillRect(b, tile, ixMax, b.xMax, b.yMin, b.yMax, b.zMin, b.zMax);

            if (b.yMin < iyMin) FillRect(b, tile, ixMin, ixMax, b.yMin, iyMin, b.zMin, b.zMax);
            if (iyMax < b.yMax) FillRect(b, tile, ixMin, ixMax, iyMax, b.yMax, b.zMin, b.zMax);

            if (b.zMin < izMin) FillRect(b, tile, ixMin, ixMax, iyMin, iyMax, b.zMin, izMin);
            if (izMax < b.zMax) FillRect(b, tile, ixMin, ixMax, iyMin, iyMax, izMax, b.zMax);
        }

        private void FillRect(BoundsInt b, TileBase tile, int xMin, int xMax, int yMin, int yMax, int zMin, int zMax)
        {
            for (int z = zMin; z < zMax; z++)
            {
                for (int y = yMin; y < yMax; y++)
                {
                    for (int x = xMin; x < xMax; x++)
                    {
                        _tilemap.SetTile(new Vector3Int(x, y, z), tile);
                    }
                }
            }
        }

        private void ClearRect(BoundsInt b, int xMin, int xMax, int yMin, int yMax, int zMin, int zMax)
        {
            for (int z = zMin; z < zMax; z++)
            {
                for (int y = yMin; y < yMax; y++)
                {
                    for (int x = xMin; x < xMax; x++)
                    {
                        _tilemap.SetTile(new Vector3Int(x, y, z), null);
                    }
                }
            }
        }

        private static bool SameBounds(in BoundsInt a, in BoundsInt b)
        {
            return a.xMin == b.xMin && a.xMax == b.xMax &&
                   a.yMin == b.yMin && a.yMax == b.yMax &&
                   a.zMin == b.zMin && a.zMax == b.zMax;
        }
    }
}
