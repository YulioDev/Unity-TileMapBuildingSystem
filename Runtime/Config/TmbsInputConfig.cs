using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TMBS.Runtime.Config
{
    [Serializable]
    public sealed class TmbsInputConfig
    {
        [Tooltip(
            "None: Input will not be processed.\n" +
            "Legacy: Uses built-in input adapters (Unity's old Input Manager).\n" +
            "InputActions: Uses Unity's Input System package with configurable InputActionReferences.")]
        public TmbsInputMode mode = TmbsInputMode.Legacy;

        // ── Legacy ──

        [Tooltip("Which legacy device adapter to use. Currently only Mouse is supported.")]
        public LegacyInputSubMode legacySubMode = LegacyInputSubMode.Mouse;

        // ── Input Actions ──

#if ENABLE_INPUT_SYSTEM
        [Tooltip("Action that provides the pointer screen position (Value, Vector2).")]
        public InputActionReference pointAction;

        [Tooltip("Button action for starting/updating/ending drags (press = DragStart, hold = DragUpdate, release = DragEnd + Confirm).")]
        public InputActionReference dragAction;

        [Tooltip("Button action for cancelling the current operation.")]
        public InputActionReference cancelAction;

        [Tooltip("Button action for Undo.")]
        public InputActionReference undoAction;

        [Tooltip("Button action for Redo.")]
        public InputActionReference redoAction;

        [Tooltip("Button held to activate alternate behaviour (e.g. erase instead of place).")]
        public InputActionReference alternateModifierAction;
#endif

        // ── Capability validation ──

        [Tooltip("If enabled, build actions will be blocked if the input adapter does not meet all required capabilities.")]
        public bool strictInputValidation = false;

        public bool requirePoint = true;
        public bool requireConfirm = true;
        public bool requireCancel = true;
        public bool requireDrag = true;
        public bool requireAlternateModifier = true;

        public bool allowUndoInput = true;
        public bool allowRedoInput = true;
    }
}