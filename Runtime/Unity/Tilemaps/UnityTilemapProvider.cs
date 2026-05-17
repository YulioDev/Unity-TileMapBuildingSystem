using UnityEngine.Tilemaps;

namespace TMBS.Unity.Tilemaps
{
    public sealed class UnityTilemapProvider : ITilemapProvider
    {
        private readonly Tilemap _visual;
        private readonly Tilemap _preview;

        public UnityTilemapProvider(Tilemap visual, Tilemap preview)
        {
            _visual = visual;
            _preview = preview;
        }

        public TilemapTarget ResolveVisual() => new TilemapTarget(_visual);

        public TilemapTarget ResolvePreview() => new TilemapTarget(_preview);
    }
}

