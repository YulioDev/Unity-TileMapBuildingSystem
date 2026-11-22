using System.Collections.Generic;
using TMBS.Runtime.Config;
using UnityEngine;

namespace TMBS.Unity.Input
{
    public static class LegacyBindingEvaluator
    {
        private static readonly TmbsInputActionId[] ShortcutActions =
        {
            TmbsInputActionId.Redo,
            TmbsInputActionId.Undo,
            TmbsInputActionId.Rotate,
            TmbsInputActionId.Mirror
        };

        public static bool IsCtrlHeld(ILegacyInputSource src) =>
            src != null && (src.GetKey(KeyCode.LeftControl) || src.GetKey(KeyCode.RightControl));

        public static bool IsShiftHeld(ILegacyInputSource src) =>
            src != null && (src.GetKey(KeyCode.LeftShift) || src.GetKey(KeyCode.RightShift));

        public static bool IsAltHeld(ILegacyInputSource src) =>
            src != null && (src.GetKey(KeyCode.LeftAlt) || src.GetKey(KeyCode.RightAlt));

        public static bool IsCommandHeld(ILegacyInputSource src) =>
            src != null && (src.GetKey(KeyCode.LeftCommand) || src.GetKey(KeyCode.RightCommand) ||
                            src.GetKey(KeyCode.LeftWindows) || src.GetKey(KeyCode.RightWindows));

        public static TmbsModifiers GetHeldModifiers(ILegacyInputSource src)
        {
            var mods = TmbsModifiers.None;
            if (IsCtrlHeld(src)) mods |= TmbsModifiers.Ctrl;
            if (IsShiftHeld(src)) mods |= TmbsModifiers.Shift;
            if (IsAltHeld(src)) mods |= TmbsModifiers.Alt;
            if (IsCommandHeld(src)) mods |= TmbsModifiers.Command;
            return mods;
        }

        public static TmbsModifiers GetSelfModifier(in TmbsKeyBinding kb)
        {
            if (kb.source != TmbsBindingSource.Key)
                return TmbsModifiers.None;

            switch (kb.key)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return TmbsModifiers.Ctrl;
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return TmbsModifiers.Shift;
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return TmbsModifiers.Alt;
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                case KeyCode.LeftWindows:
                case KeyCode.RightWindows:
                    return TmbsModifiers.Command;
                default:
                    return TmbsModifiers.None;
            }
        }

        public static bool ModifiersMatch(ILegacyInputSource src, in TmbsKeyBinding kb, bool allowUnrequestedShift = true)
        {
            if (src == null || !kb.IsBound)
                return false;

            var held = GetHeldModifiers(src);
            var selfMod = GetSelfModifier(in kb);

            if ((kb.modifiers & TmbsModifiers.Ctrl) != 0 && (held & TmbsModifiers.Ctrl) == 0) return false;
            if ((kb.modifiers & TmbsModifiers.Shift) != 0 && (held & TmbsModifiers.Shift) == 0) return false;
            if ((kb.modifiers & TmbsModifiers.Alt) != 0 && (held & TmbsModifiers.Alt) == 0) return false;
            if ((kb.modifiers & TmbsModifiers.Command) != 0 && (held & TmbsModifiers.Command) == 0) return false;

            var extraMods = held & ~kb.modifiers & ~selfMod;

            if ((extraMods & (TmbsModifiers.Ctrl | TmbsModifiers.Alt | TmbsModifiers.Command)) != 0)
                return false;

            if (!allowUnrequestedShift && (extraMods & TmbsModifiers.Shift) != 0)
                return false;

            return true;
        }

        public static bool IsTriggerPressed(ILegacyInputSource src, in TmbsKeyBinding kb)
        {
            if (src == null || !kb.IsBound)
                return false;

            return kb.source == TmbsBindingSource.Key
                ? src.GetKeyDown(kb.key)
                : src.GetMouseButtonDown(kb.mouseButton);
        }

        public static bool IsTriggerHeld(ILegacyInputSource src, in TmbsKeyBinding kb)
        {
            if (src == null || !kb.IsBound)
                return false;

            return kb.source == TmbsBindingSource.Key
                ? src.GetKey(kb.key)
                : src.GetMouseButton(kb.mouseButton);
        }

        public static bool IsTriggerReleased(ILegacyInputSource src, in TmbsKeyBinding kb)
        {
            if (src == null || !kb.IsBound)
                return false;

            return kb.source == TmbsBindingSource.Key
                ? src.GetKeyUp(kb.key)
                : src.GetMouseButtonUp(kb.mouseButton);
        }

        public static bool WasKeyBindingPressed(ILegacyInputSource src, in TmbsKeyBinding kb, bool allowUnrequestedShift = true)
        {
            return IsTriggerPressed(src, in kb) && ModifiersMatch(src, in kb, allowUnrequestedShift);
        }

        public static bool IsModifierBindingHeld(ILegacyInputSource src, in TmbsActionBinding binding)
        {
            return IsKeyBindingHeldWithModifiers(src, in binding.primary)
                   || IsKeyBindingHeldWithModifiers(src, in binding.secondary);
        }

        private static bool IsKeyBindingHeldWithModifiers(ILegacyInputSource src, in TmbsKeyBinding kb)
        {
            return IsTriggerHeld(src, in kb) && ModifiersMatch(src, in kb, allowUnrequestedShift: true);
        }

        public static bool WasActionPressed(ILegacyInputSource src, in TmbsActionBinding binding, bool allowUnrequestedShift = true)
        {
            return WasKeyBindingPressed(src, in binding.primary, allowUnrequestedShift)
                   || WasKeyBindingPressed(src, in binding.secondary, allowUnrequestedShift);
        }

        public static bool IsActionTriggerHeld(ILegacyInputSource src, in TmbsActionBinding binding)
        {
            return IsTriggerHeld(src, in binding.primary) || IsTriggerHeld(src, in binding.secondary);
        }

        public static bool WasActionTriggerReleased(ILegacyInputSource src, in TmbsActionBinding binding)
        {
            return IsTriggerReleased(src, in binding.primary) || IsTriggerReleased(src, in binding.secondary);
        }

        public static void EvaluateShortcuts(
            ILegacyInputSource src,
            TmbsLegacyBindings bindings,
            List<TmbsInputActionId> triggeredOut)
        {
            triggeredOut.Clear();
            if (src == null || bindings == null)
                return;

            for (int a = 0; a < ShortcutActions.Length; a++)
            {
                var actionId = ShortcutActions[a];
                var actionBinding = bindings.Get(actionId);

                if (MatchesShortcutSlot(src, bindings, actionId, in actionBinding.primary)
                    || MatchesShortcutSlot(src, bindings, actionId, in actionBinding.secondary))
                {
                    triggeredOut.Add(actionId);
                }
            }
        }

        private static bool MatchesShortcutSlot(
            ILegacyInputSource src,
            TmbsLegacyBindings bindings,
            TmbsInputActionId actionId,
            in TmbsKeyBinding slotBinding)
        {
            if (!WasKeyBindingPressed(src, in slotBinding, allowUnrequestedShift: true))
                return false;

            int myModCount = slotBinding.ModifierCount;

            for (int otherIdx = 0; otherIdx < ShortcutActions.Length; otherIdx++)
            {
                var otherId = ShortcutActions[otherIdx];
                if (otherId == actionId)
                    continue;

                var otherBinding = bindings.Get(otherId);
                if (HasMoreSpecificMatch(src, in slotBinding, myModCount, in otherBinding.primary)
                    || HasMoreSpecificMatch(src, in slotBinding, myModCount, in otherBinding.secondary))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasMoreSpecificMatch(
            ILegacyInputSource src,
            in TmbsKeyBinding current,
            int currentModCount,
            in TmbsKeyBinding candidate)
        {
            if (!candidate.IsBound || candidate.ModifierCount <= currentModCount)
                return false;

            if (!current.SameTrigger(in candidate))
                return false;

            return WasKeyBindingPressed(src, in candidate, allowUnrequestedShift: true);
        }
    }
}
