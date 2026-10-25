using TMBS.Core.Focus;
using TMBS.Core.Intents;
using UnityEngine.EventSystems;

namespace TMBS.Unity.Input
{
    public sealed class UiFocusGuard : IInputFocusService
    {
        public bool CanConsumeMutating(string instanceId, BuildIntent intent)
        {
            if (TMBS.Runtime.Debugging.TmbsDebugMenu.IsPointerOverAnyMenu)
                return false;

            var eventSystem = EventSystem.current;

            if (eventSystem == null)
                return true;

            return !eventSystem.IsPointerOverGameObject();
        }
    }
}
