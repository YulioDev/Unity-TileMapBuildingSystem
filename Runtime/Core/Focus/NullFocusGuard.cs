using TMBS.Core.Intents;

namespace TMBS.Core.Focus
{
    public sealed class NullFocusGuard : IInputFocusService
    {
        public bool CanConsumeMutating(string instanceId, BuildIntent intent) => true;
    }
}
