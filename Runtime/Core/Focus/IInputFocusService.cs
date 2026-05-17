using TMBS.Core.Intents;

namespace TMBS.Core.Focus
{
    public interface IInputFocusService
    {
        bool CanConsumeMutating(string instanceId, BuildIntent intent);
    }
}

