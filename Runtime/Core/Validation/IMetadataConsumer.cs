using TMBS.Core.Metadata;

namespace TMBS.Core.Validation
{
    public interface IMetadataConsumer
    {
        void Inject(IMetadataStore metadata);
    }
}
