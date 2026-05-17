namespace TMBS.Core.Catalog
{
    public interface IBuildableCatalog
    {
        bool TryGet(int id, out BuildableDefinition definition);
    }
}

