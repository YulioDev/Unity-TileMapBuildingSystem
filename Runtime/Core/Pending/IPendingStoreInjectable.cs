namespace TMBS.Core.Pending
{
    public interface IPendingStoreInjectable
    {
        void InjectStore(IPendingConstructionStore store);
    }
}