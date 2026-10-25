namespace TMBS.Core.Input
{
    public readonly struct InputAdapterContext
    {
        public readonly string InstanceId;

        public InputAdapterContext(string instanceId)
        {
            InstanceId = instanceId;
        }
    }

    public interface IInputAdapterFactory
    {
        bool TryCreateInputAdapter(InputAdapterContext context, out IBuildInputAdapter adapter);
        void ReleaseInputAdapter(IBuildInputAdapter adapter);
    }
}
