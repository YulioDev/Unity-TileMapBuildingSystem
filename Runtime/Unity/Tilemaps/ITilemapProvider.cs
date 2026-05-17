namespace TMBS.Unity.Tilemaps
{
    public interface ITilemapProvider
    {
        TilemapTarget ResolveVisual();
        TilemapTarget ResolvePreview();
    }
}

