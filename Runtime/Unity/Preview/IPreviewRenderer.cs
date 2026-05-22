using UnityEngine;
using UnityEngine.Tilemaps;
using TMBS.Core.Validation;

namespace TMBS.Unity.Preview
{
    public interface IPreviewRenderer
    {
        void ShowCell(Vector3Int cell, bool valid);
        void ShowRect(BoundsInt bounds, bool valid);
        void ShowRectMasked(BoundsInt fullArea, CellMask blockedMask);
        void Hide();
        void UpdateTiles(TileBase valid, TileBase invalid);
    }
}