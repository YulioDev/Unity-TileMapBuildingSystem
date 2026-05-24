using TMBS.Core.Focus;
using TMBS.Core.Intents;
using UnityEngine.EventSystems;

namespace TMBS.Unity.Input
{
    public sealed class UiFocusGuardService : IInputFocusService
    {
        public bool CanConsumeMutating(string instanceId, BuildIntent intent)
        {
            var eventSystem = EventSystem.current;

            if (eventSystem == null)
                return true;

            return !eventSystem.IsPointerOverGameObject();
        }
    }
}
