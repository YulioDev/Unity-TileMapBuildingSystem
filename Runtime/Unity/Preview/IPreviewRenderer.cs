using UnityEngine;

namespace TMBS.Unity.Preview
{
    public interface IPreviewRenderer
    {
        void ShowCell(Vector3Int cell, bool valid);
        void ShowRect(BoundsInt bounds, bool valid);
        void Hide();
    }
}

