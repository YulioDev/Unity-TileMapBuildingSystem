namespace TMBS.Core.Pending
{
    public interface IPendingStoreConsumer
    {
        void InjectStore(IPendingStore store);
    }
}
