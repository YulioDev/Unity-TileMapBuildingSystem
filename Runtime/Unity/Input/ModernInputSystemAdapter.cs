#if ENABLE_INPUT_SYSTEM
using System;
using TMBS.Core.Input;
using TMBS.Core.Intents;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMBS.Unity.Input
{
    public sealed class ModernInputSystemAdapter : MonoBehaviour, IBuildInputAdapter
    {
        [SerializeField] private InputActionReference point;
        [SerializeField] private InputActionReference confirm;
        [SerializeField] private InputActionReference cancel;
        [SerializeField] private InputActionReference alternate;
        [SerializeField] private InputActionReference undo;
        [SerializeField] private InputActionReference redo;
        [SerializeField] private Camera worldCamera;

        public InputCapabilities Capabilities => new InputCapabilities(
            hasPoint: point != null,
            hasConfirm: confirm != null,
            hasCancel: cancel != null,
            hasDrag: true,
            hasUndo: undo != null,
            hasRedo: redo != null,
            hasPipette: false,
            hasAlternateModifier: alternate != null);

#pragma warning disable CS0067
        public event Action<BuildIntent> BuildIntentRaised;
#pragma warning restore CS0067

        public void Enable()
        {
            point?.action?.Enable();
            confirm?.action?.Enable();
            cancel?.action?.Enable();
            alternate?.action?.Enable();
            undo?.action?.Enable();
            redo?.action?.Enable();
        }

        public void Disable()
        {
            point?.action?.Disable();
            confirm?.action?.Disable();
            cancel?.action?.Disable();
            alternate?.action?.Disable();
            undo?.action?.Disable();
            redo?.action?.Disable();
        }
    }
}
#endif
