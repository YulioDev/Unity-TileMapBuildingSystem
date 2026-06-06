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

            if (eventSystem.IsPointerOverGameObject())
                return false;

            int touchCount = UnityEngine.Input.touchCount;
            for (int i = 0; i < touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);

                if (touch.phase == UnityEngine.TouchPhase.Ended ||
                    touch.phase == UnityEngine.TouchPhase.Canceled)
                {
                    continue;
                }

                if (eventSystem.IsPointerOverGameObject(touch.fingerId))
                    return false;
            }

            return true;
        }
    }
}
