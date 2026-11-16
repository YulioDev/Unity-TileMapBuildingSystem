using System;
using TMBS.Core.Focus;
using TMBS.Core.Intents;
using UnityEngine.EventSystems;

namespace TMBS.Unity.Input
{
    public sealed class UiFocusGuard : IInputFocusService
    {
        public static Func<bool> IsPointerBlockedByOverlay { get; set; }

        public bool CanConsumeMutating(string instanceId, BuildIntent intent)
        {
            if (IsPointerBlockedByOverlay != null && IsPointerBlockedByOverlay())
                return false;

            var eventSystem = EventSystem.current;

            if (eventSystem == null)
                return true;

            return !eventSystem.IsPointerOverGameObject();
        }
    }
}
