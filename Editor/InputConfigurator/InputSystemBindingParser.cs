#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using TMBS.Runtime.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMBS.Editor.InputConfigurator
{
    public static class InputSystemBindingParser
    {
        public static TmbsLegacyBindings ParseFromConfig(TmbsInputConfig inputConfig)
        {
            var result = new TmbsLegacyBindings();
            if (inputConfig == null)
                return result;

            result.place = ParseActionRef(inputConfig.dragAction);
            result.cancel = ParseActionRef(inputConfig.cancelAction);
            result.eraseModifier = ParseActionRef(inputConfig.alternateModifierAction);
            result.undo = ParseActionRef(inputConfig.undoAction);
            result.redo = ParseActionRef(inputConfig.redoAction);
            result.rotate = ParseActionRef(inputConfig.rotateAction);
            result.mirror = ParseActionRef(inputConfig.mirrorAction);

            return result;
        }

        private static TmbsActionBinding ParseActionRef(InputActionReference actionRef)
        {
            var action = actionRef != null ? actionRef.action : null;
            if (action == null)
                return default;

            var parsed = new List<TmbsKeyBinding>(2);
            var bindings = action.bindings;

            for (int i = 0; i < bindings.Count && parsed.Count < 2; i++)
            {
                var b = bindings[i];
                if (b.isComposite)
                {
                    var mod = TmbsModifiers.None;
                    TmbsKeyBinding trigger = default;

                    int j = i + 1;
                    while (j < bindings.Count && bindings[j].isPartOfComposite)
                    {
                        var part = bindings[j];
                        string partName = part.name != null ? part.name.ToLowerInvariant() : string.Empty;
                        if (partName.StartsWith("modifier"))
                        {
                            mod |= PathToModifier(part.effectivePath);
                        }
                        else if (partName == "binding")
                        {
                            trigger = PathToBinding(part.effectivePath);
                        }
                        j++;
                    }

                    i = j - 1;

                    if (trigger.IsBound)
                    {
                        trigger.modifiers |= mod;
                        parsed.Add(trigger);
                    }
                }
                else if (!b.isPartOfComposite)
                {
                    var kb = PathToBinding(b.effectivePath);
                    if (kb.IsBound)
                        parsed.Add(kb);
                }
            }

            return new TmbsActionBinding
            {
                primary = parsed.Count > 0 ? parsed[0] : default,
                secondary = parsed.Count > 1 ? parsed[1] : default
            };
        }

        private static TmbsModifiers PathToModifier(string path)
        {
            if (string.IsNullOrEmpty(path))
                return TmbsModifiers.None;

            string lower = path.ToLowerInvariant();
            if (lower.Contains("ctrl") || lower.Contains("control")) return TmbsModifiers.Ctrl;
            if (lower.Contains("shift")) return TmbsModifiers.Shift;
            if (lower.Contains("alt")) return TmbsModifiers.Alt;
            if (lower.Contains("meta") || lower.Contains("command") || lower.Contains("apple") || lower.Contains("windows"))
                return TmbsModifiers.Command;
            return TmbsModifiers.None;
        }

        public static TmbsKeyBinding PathToBinding(string path)
        {
            if (string.IsNullOrEmpty(path))
                return default;

            string lower = path.ToLowerInvariant();

            if (lower.StartsWith("<mouse>/"))
            {
                string ctrl = lower.Substring("<mouse>/".Length);
                switch (ctrl)
                {
                    case "leftbutton":
                    case "press":
                        return TmbsKeyBinding.Mouse(0);
                    case "rightbutton":
                        return TmbsKeyBinding.Mouse(1);
                    case "middlebutton":
                        return TmbsKeyBinding.Mouse(2);
                    case "backbutton":
                        return TmbsKeyBinding.Mouse(3);
                    case "forwardbutton":
                        return TmbsKeyBinding.Mouse(4);
                    default:
                        return default;
                }
            }

            if (lower.StartsWith("<pointer>/"))
            {
                string ctrl = lower.Substring("<pointer>/".Length);
                if (ctrl == "press")
                    return TmbsKeyBinding.Mouse(0);
                return default;
            }

            if (!lower.StartsWith("<keyboard>/"))
                return default;

            string keyName = lower.Substring("<keyboard>/".Length);

            if (keyName.Length == 1)
            {
                char ch = keyName[0];
                if (ch >= 'a' && ch <= 'z')
                    return TmbsKeyBinding.Key((KeyCode)((int)KeyCode.A + (ch - 'a')));
                if (ch >= '0' && ch <= '9')
                    return TmbsKeyBinding.Key((KeyCode)((int)KeyCode.Alpha0 + (ch - '0')));
            }

            if (keyName.StartsWith("f") && int.TryParse(keyName.Substring(1), out int fNum) && fNum >= 1 && fNum <= 12)
                return TmbsKeyBinding.Key((KeyCode)((int)KeyCode.F1 + (fNum - 1)));

            if (keyName.StartsWith("numpad") && keyName.Length == 7 && keyName[6] >= '0' && keyName[6] <= '9')
                return TmbsKeyBinding.Key((KeyCode)((int)KeyCode.Keypad0 + (keyName[6] - '0')));

            switch (keyName)
            {
                case "escape": return TmbsKeyBinding.Key(KeyCode.Escape);
                case "space": return TmbsKeyBinding.Key(KeyCode.Space);
                case "enter": return TmbsKeyBinding.Key(KeyCode.Return);
                case "numpadenter": return TmbsKeyBinding.Key(KeyCode.KeypadEnter);
                case "tab": return TmbsKeyBinding.Key(KeyCode.Tab);
                case "backspace": return TmbsKeyBinding.Key(KeyCode.Backspace);
                case "delete": return TmbsKeyBinding.Key(KeyCode.Delete);
                case "insert": return TmbsKeyBinding.Key(KeyCode.Insert);
                case "home": return TmbsKeyBinding.Key(KeyCode.Home);
                case "end": return TmbsKeyBinding.Key(KeyCode.End);
                case "pageup": return TmbsKeyBinding.Key(KeyCode.PageUp);
                case "pagedown": return TmbsKeyBinding.Key(KeyCode.PageDown);
                case "capslock": return TmbsKeyBinding.Key(KeyCode.CapsLock);
                case "backquote": return TmbsKeyBinding.Key(KeyCode.BackQuote);
                case "quote": return TmbsKeyBinding.Key(KeyCode.Quote);
                case "semicolon": return TmbsKeyBinding.Key(KeyCode.Semicolon);
                case "comma": return TmbsKeyBinding.Key(KeyCode.Comma);
                case "period": return TmbsKeyBinding.Key(KeyCode.Period);
                case "slash": return TmbsKeyBinding.Key(KeyCode.Slash);
                case "backslash": return TmbsKeyBinding.Key(KeyCode.Backslash);
                case "leftbracket": return TmbsKeyBinding.Key(KeyCode.LeftBracket);
                case "rightbracket": return TmbsKeyBinding.Key(KeyCode.RightBracket);
                case "minus": return TmbsKeyBinding.Key(KeyCode.Minus);
                case "equals": return TmbsKeyBinding.Key(KeyCode.Equals);
                case "uparrow": return TmbsKeyBinding.Key(KeyCode.UpArrow);
                case "downarrow": return TmbsKeyBinding.Key(KeyCode.DownArrow);
                case "leftarrow": return TmbsKeyBinding.Key(KeyCode.LeftArrow);
                case "rightarrow": return TmbsKeyBinding.Key(KeyCode.RightArrow);
                case "leftshift":
                case "shift":
                    return TmbsKeyBinding.Key(KeyCode.LeftShift);
                case "rightshift":
                    return TmbsKeyBinding.Key(KeyCode.RightShift);
                case "leftctrl":
                case "ctrl":
                    return TmbsKeyBinding.Key(KeyCode.LeftControl);
                case "rightctrl":
                    return TmbsKeyBinding.Key(KeyCode.RightControl);
                case "leftalt":
                case "alt":
                    return TmbsKeyBinding.Key(KeyCode.LeftAlt);
                case "rightalt":
                case "altgr":
                    return TmbsKeyBinding.Key(KeyCode.RightAlt);
                case "leftmeta":
                case "leftcommand":
                case "leftapple":
                case "leftwindows":
                    return TmbsKeyBinding.Key(KeyCode.LeftCommand);
                case "rightmeta":
                case "rightcommand":
                case "rightapple":
                case "rightwindows":
                    return TmbsKeyBinding.Key(KeyCode.RightCommand);
                default:
                    return default;
            }
        }
    }
}
#endif
