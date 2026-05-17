using System.Collections.Generic;

namespace TMBS.Core.Catalog
{
    public sealed class DefaultBuildableCatalog : IBuildableCatalog
    {
        private readonly Dictionary<int, BuildableDefinition> _definitions = new Dictionary<int, BuildableDefinition>();

        public void Register(BuildableDefinition definition)
        {
            _definitions[definition.Id] = definition;
        }

        public bool TryGet(int id, out BuildableDefinition definition)
        {
            return _definitions.TryGetValue(id, out definition);
        }
    }
}

