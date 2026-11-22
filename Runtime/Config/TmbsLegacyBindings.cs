using System;
using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    public enum TmbsInputActionId
    {
        Place = 0,
        Cancel = 1,
        EraseModifier = 2,
        Undo = 3,
        Redo = 4,
        Rotate = 5,
        Mirror = 6
    }

    public enum TmbsBindingSource
    {
        None = 0,
        Key = 1,
        MouseButton = 2
    }

    [Flags]
    public enum TmbsModifiers
    {
        None = 0,
        Ctrl = 1 << 0,
        Shift = 1 << 1,
        Alt = 1 << 2,
        Command = 1 << 3
    }

    [Serializable]
    public struct TmbsKeyBinding
    {
        public TmbsBindingSource source;
        public KeyCode key;
        public int mouseButton;
        public TmbsModifiers modifiers;

        public bool IsBound =>
            source == TmbsBindingSource.Key
                ? key != KeyCode.None
                : source == TmbsBindingSource.MouseButton && mouseButton >= 0;

        public int ModifierCount
        {
            get
            {
                int count = 0;
                if ((modifiers & TmbsModifiers.Ctrl) != 0) count++;
                if ((modifiers & TmbsModifiers.Shift) != 0) count++;
                if ((modifiers & TmbsModifiers.Alt) != 0) count++;
                if ((modifiers & TmbsModifiers.Command) != 0) count++;
                return count;
            }
        }

        public static TmbsKeyBinding None => default;

        public static TmbsKeyBinding Key(KeyCode keyCode, TmbsModifiers modifiers = TmbsModifiers.None)
        {
            if (keyCode == KeyCode.None)
                return default;

            return new TmbsKeyBinding
            {
                source = TmbsBindingSource.Key,
                key = keyCode,
                mouseButton = -1,
                modifiers = modifiers
            };
        }

        public static TmbsKeyBinding Mouse(int button, TmbsModifiers modifiers = TmbsModifiers.None)
        {
            if (button < 0)
                return default;

            return new TmbsKeyBinding
            {
                source = TmbsBindingSource.MouseButton,
                key = KeyCode.None,
                mouseButton = button,
                modifiers = modifiers
            };
        }

        public bool SameTrigger(in TmbsKeyBinding other)
        {
            if (!IsBound || !other.IsBound || source != other.source)
                return false;

            return source == TmbsBindingSource.Key
                ? key == other.key
                : mouseButton == other.mouseButton;
        }

        public bool SameInput(in TmbsKeyBinding other)
        {
            return SameTrigger(in other) && modifiers == other.modifiers;
        }

        public string ToDisplayString()
        {
            if (!IsBound)
                return "Unbound";

            var parts = new List<string>(5);
            if ((modifiers & TmbsModifiers.Ctrl) != 0) parts.Add("Ctrl");
            if ((modifiers & TmbsModifiers.Command) != 0) parts.Add("Cmd");
            if ((modifiers & TmbsModifiers.Alt) != 0) parts.Add("Alt");
            if ((modifiers & TmbsModifiers.Shift) != 0) parts.Add("Shift");

            if (source == TmbsBindingSource.MouseButton)
                parts.Add(FormatMouseButton(mouseButton));
            else
                parts.Add(FormatKeyCode(key));

            return string.Join("+", parts);
        }

        public override string ToString() => ToDisplayString();

        public static string FormatMouseButton(int button)
        {
            switch (button)
            {
                case 0: return "LMB";
                case 1: return "RMB";
                case 2: return "MMB";
                case 3: return "Mouse 4";
                case 4: return "Mouse 5";
                default: return $"Mouse {button + 1}";
            }
        }

        public static string FormatKeyCode(KeyCode code)
        {
            if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9)
                return ((int)(code - KeyCode.Alpha0)).ToString();

            if (code >= KeyCode.Keypad0 && code <= KeyCode.Keypad9)
                return "Num " + ((int)(code - KeyCode.Keypad0));

            switch (code)
            {
                case KeyCode.None: return "None";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "Num Enter";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.Delete: return "Del";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.CapsLock: return "Caps";
                case KeyCode.LeftShift: return "LShift";
                case KeyCode.RightShift: return "RShift";
                case KeyCode.LeftControl: return "LCtrl";
                case KeyCode.RightControl: return "RCtrl";
                case KeyCode.LeftAlt: return "LAlt";
                case KeyCode.RightAlt: return "RAlt";
                case KeyCode.LeftCommand: return "LCmd";
                case KeyCode.RightCommand: return "RCmd";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                default: return code.ToString();
            }
        }
    }

    [Serializable]
    public struct TmbsActionBinding
    {
        public TmbsKeyBinding primary;
        public TmbsKeyBinding secondary;

        public bool IsBound => primary.IsBound || secondary.IsBound;

        public TmbsKeyBinding GetSlot(int slot)
        {
            return slot == 0 ? primary : secondary;
        }

        public void SetSlot(int slot, TmbsKeyBinding value)
        {
            if (slot == 0)
                primary = value;
            else
                secondary = value;
        }

        public string ToSummaryString()
        {
            if (primary.IsBound && secondary.IsBound)
                return $"{primary.ToDisplayString()} / {secondary.ToDisplayString()}";
            if (primary.IsBound)
                return primary.ToDisplayString();
            if (secondary.IsBound)
                return secondary.ToDisplayString();
            return "Unbound";
        }
    }

    [Serializable]
    public sealed class TmbsLegacyBindings
    {
        public static readonly TmbsInputActionId[] AllActionIds =
        {
            TmbsInputActionId.Place,
            TmbsInputActionId.Cancel,
            TmbsInputActionId.EraseModifier,
            TmbsInputActionId.Undo,
            TmbsInputActionId.Redo,
            TmbsInputActionId.Rotate,
            TmbsInputActionId.Mirror
        };

        public TmbsActionBinding place;
        public TmbsActionBinding cancel;
        public TmbsActionBinding eraseModifier;
        public TmbsActionBinding undo;
        public TmbsActionBinding redo;
        public TmbsActionBinding rotate;
        public TmbsActionBinding mirror;

        public TmbsActionBinding Get(TmbsInputActionId id)
        {
            switch (id)
            {
                case TmbsInputActionId.Place: return place;
                case TmbsInputActionId.Cancel: return cancel;
                case TmbsInputActionId.EraseModifier: return eraseModifier;
                case TmbsInputActionId.Undo: return undo;
                case TmbsInputActionId.Redo: return redo;
                case TmbsInputActionId.Rotate: return rotate;
                case TmbsInputActionId.Mirror: return mirror;
                default: return default;
            }
        }

        public void Set(TmbsInputActionId id, TmbsActionBinding value)
        {
            switch (id)
            {
                case TmbsInputActionId.Place: place = value; break;
                case TmbsInputActionId.Cancel: cancel = value; break;
                case TmbsInputActionId.EraseModifier: eraseModifier = value; break;
                case TmbsInputActionId.Undo: undo = value; break;
                case TmbsInputActionId.Redo: redo = value; break;
                case TmbsInputActionId.Rotate: rotate = value; break;
                case TmbsInputActionId.Mirror: mirror = value; break;
            }
        }

        public TmbsKeyBinding GetSlot(TmbsInputActionId id, int slot)
        {
            return Get(id).GetSlot(slot);
        }

        public void SetSlot(TmbsInputActionId id, int slot, TmbsKeyBinding value)
        {
            var binding = Get(id);
            binding.SetSlot(slot, value);
            Set(id, binding);
        }

        public TmbsLegacyBindings Clone()
        {
            var copy = new TmbsLegacyBindings();
            copy.CopyFrom(this);
            return copy;
        }

        public void CopyFrom(TmbsLegacyBindings other)
        {
            if (other == null)
                return;

            place = other.place;
            cancel = other.cancel;
            eraseModifier = other.eraseModifier;
            undo = other.undo;
            redo = other.redo;
            rotate = other.rotate;
            mirror = other.mirror;
        }

        public static TmbsLegacyBindings CreateDefault()
        {
            return new TmbsLegacyBindings
            {
                place = new TmbsActionBinding
                {
                    primary = TmbsKeyBinding.Mouse(0),
                    secondary = TmbsKeyBinding.None
                },
                cancel = new TmbsActionBinding
                {
                    primary = TmbsKeyBinding.Mouse(1),
                    secondary = TmbsKeyBinding.None
                },
                eraseModifier = new TmbsActionBinding
                {
                    primary = TmbsKeyBinding.Key(KeyCode.LeftShift),
                    secondary = TmbsKeyBinding.Key(KeyCode.RightShift)
                },
                undo = new TmbsActionBinding
                {
                    primary = TmbsKeyBinding.Key(KeyCode.Z, TmbsModifiers.Ctrl),
                    secondary = TmbsKeyBinding.None
                },
                redo = new TmbsActionBinding
                {
                    primary = TmbsKeyBinding.Key(KeyCode.Y, TmbsModifiers.Ctrl),
                    secondary = TmbsKeyBinding.Key(KeyCode.Z, TmbsModifiers.Ctrl | TmbsModifiers.Shift)
                },
                rotate = new TmbsActionBinding
                {
                    primary = TmbsKeyBinding.Key(KeyCode.R),
                    secondary = TmbsKeyBinding.None
                },
                mirror = new TmbsActionBinding
                {
                    primary = TmbsKeyBinding.Key(KeyCode.F),
                    secondary = TmbsKeyBinding.None
                }
            };
        }

        public int CountBoundActions()
        {
            int count = 0;
            for (int i = 0; i < AllActionIds.Length; i++)
            {
                if (Get(AllActionIds[i]).IsBound)
                    count++;
            }
            return count;
        }

        public bool HasConflict(
            TmbsInputActionId actionId,
            int slot,
            out TmbsInputActionId conflictingAction,
            out int conflictingSlot)
        {
            var target = GetSlot(actionId, slot);
            if (!target.IsBound)
            {
                conflictingAction = default;
                conflictingSlot = -1;
                return false;
            }

            for (int i = 0; i < AllActionIds.Length; i++)
            {
                var otherId = AllActionIds[i];
                var otherBinding = Get(otherId);
                for (int otherSlot = 0; otherSlot < 2; otherSlot++)
                {
                    if (otherId == actionId && otherSlot == slot)
                        continue;

                    var candidate = otherBinding.GetSlot(otherSlot);
                    if (candidate.IsBound && target.SameInput(in candidate))
                    {
                        conflictingAction = otherId;
                        conflictingSlot = otherSlot;
                        return true;
                    }
                }
            }

            conflictingAction = default;
            conflictingSlot = -1;
            return false;
        }

        public int CountConflictingSlots()
        {
            int conflicts = 0;
            for (int i = 0; i < AllActionIds.Length; i++)
            {
                var actionId = AllActionIds[i];
                for (int slot = 0; slot < 2; slot++)
                {
                    if (HasConflict(actionId, slot, out _, out _))
                        conflicts++;
                }
            }
            return conflicts;
        }
    }
}
