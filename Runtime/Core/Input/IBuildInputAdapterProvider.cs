namespace TMBS.Core.Input
{
    public readonly struct BuildInputAdapterContext
    {
        public readonly string InstanceId;

        public BuildInputAdapterContext(string instanceId)
        {
            InstanceId = instanceId;
        }
    }

    public interface IBuildInputAdapterProvider
    {
        bool TryCreateInputAdapter(in BuildInputAdapterContext context, out IBuildInputAdapter adapter);

        void ReleaseInputAdapter(IBuildInputAdapter adapter);
    }
}