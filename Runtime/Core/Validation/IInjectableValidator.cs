using TMBS.Core.Metadata;

namespace TMBS.Core.Validation
{
    public interface IInjectableValidator
    {
        void Inject(IMetadataStore metadata);
    }
}
