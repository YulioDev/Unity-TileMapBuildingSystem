#if ENABLE_INPUT_SYSTEM
using System;
using System.IO;
using System.Linq;
using TMBS.Runtime.Config;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TMBS.Editor.InputConfigurator
{
    public static class TmbsInputActionsGenerator
    {
        public const string DefaultFolder = "Assets/TMBS/Input";
        public const string DefaultAssetPath = "Assets/TMBS/Input/TMBS_Actions.inputactions";
        public const string ActionMapName = "TMBS Building";

        public static InputActionAsset GenerateAndAssign(
            TmbsRootConfig config,
            string assetPath,
            TmbsLegacyBindings template = null)
        {
            if (string.IsNullOrEmpty(assetPath))
                assetPath = DefaultAssetPath;

            var bindingsTemplate = template ?? TmbsInputPresets.CreateBindings(TmbsInputPresetId.ClassicPc);

            string directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var tempAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            tempAsset.name = Path.GetFileNameWithoutExtension(assetPath);

            try
            {
                var map = tempAsset.AddActionMap(ActionMapName);

                var point = map.AddAction(
                    "Point",
                    InputActionType.Value,
                    "<Pointer>/position",
                    expectedControlLayout: "Vector2");
                point.AddBinding("<Mouse>/position");

                AddActionFromTemplate(map, "Place", bindingsTemplate.place, "<Mouse>/leftButton");
                AddActionFromTemplate(map, "Cancel", bindingsTemplate.cancel, "<Mouse>/rightButton");
                AddActionFromTemplate(map, "Undo", bindingsTemplate.undo, null);
                AddActionFromTemplate(map, "Redo", bindingsTemplate.redo, null);
                AddActionFromTemplate(map, "AlternateModifier", bindingsTemplate.eraseModifier, "<Keyboard>/leftShift");
                AddActionFromTemplate(map, "Rotate", bindingsTemplate.rotate, "<Keyboard>/r");
                AddActionFromTemplate(map, "Mirror", bindingsTemplate.mirror, "<Keyboard>/f");

                string json = tempAsset.ToJson();
                File.WriteAllText(assetPath, json);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempAsset);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importedAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);
            var refs = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<InputActionReference>()
                .ToList();

            if (config != null && config.input != null)
            {
                Undo.RecordObject(config, "Generate and Assign TMBS Input Actions");
                config.input.pointAction = FindRef(refs, "Point");
                config.input.dragAction = FindRef(refs, "Place");
                config.input.cancelAction = FindRef(refs, "Cancel");
                config.input.undoAction = FindRef(refs, "Undo");
                config.input.redoAction = FindRef(refs, "Redo");
                config.input.alternateModifierAction = FindRef(refs, "AlternateModifier");
                config.input.rotateAction = FindRef(refs, "Rotate");
                config.input.mirrorAction = FindRef(refs, "Mirror");
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            return importedAsset;
        }

        private static InputActionReference FindRef(System.Collections.Generic.List<InputActionReference> refs, string actionName)
        {
            for (int i = 0; i < refs.Count; i++)
            {
                var r = refs[i];
                if (r != null && r.action != null && string.Equals(r.action.name, actionName, StringComparison.Ordinal))
                    return r;
            }
            return null;
        }

        private static void AddActionFromTemplate(
            InputActionMap map,
            string actionName,
            TmbsActionBinding binding,
            string fallbackPath)
        {
            var action = map.AddAction(actionName, InputActionType.Button);
            bool anyAdded = false;

            if (binding.primary.IsBound)
                anyAdded |= AddSlotBinding(action, binding.primary);

            if (binding.secondary.IsBound)
                anyAdded |= AddSlotBinding(action, binding.secondary);

            if (!anyAdded && !string.IsNullOrEmpty(fallbackPath))
            {
                action.AddBinding(fallbackPath);
            }
        }

        private static bool AddSlotBinding(InputAction action, TmbsKeyBinding kb)
        {
            string triggerPath = ToControlPath(kb);
            if (string.IsNullOrEmpty(triggerPath))
                return false;

            var modPaths = CollectModifierPaths(kb.modifiers);
            if (modPaths.Count == 0)
            {
                action.AddBinding(triggerPath);
                return true;
            }

            if (modPaths.Count == 1)
            {
                action.AddCompositeBinding("OneModifier")
                    .With("modifier", modPaths[0])
                    .With("binding", triggerPath);
                return true;
            }

            action.AddCompositeBinding("TwoModifiers")
                .With("modifier1", modPaths[0])
                .With("modifier2", modPaths[1])
                .With("binding", triggerPath);
            return true;
        }

        private static System.Collections.Generic.List<string> CollectModifierPaths(TmbsModifiers mods)
        {
            var list = new System.Collections.Generic.List<string>(4);
            if ((mods & TmbsModifiers.Ctrl) != 0) list.Add("<Keyboard>/ctrl");
            if ((mods & TmbsModifiers.Command) != 0) list.Add("<Keyboard>/leftMeta");
            if ((mods & TmbsModifiers.Shift) != 0) list.Add("<Keyboard>/shift");
            if ((mods & TmbsModifiers.Alt) != 0) list.Add("<Keyboard>/alt");
            return list;
        }

        public static string ToControlPath(TmbsKeyBinding kb)
        {
            if (!kb.IsBound)
                return null;

            if (kb.source == TmbsBindingSource.MouseButton)
            {
                switch (kb.mouseButton)
                {
                    case 0: return "<Mouse>/leftButton";
                    case 1: return "<Mouse>/rightButton";
                    case 2: return "<Mouse>/middleButton";
                    case 3: return "<Mouse>/backButton";
                    case 4: return "<Mouse>/forwardButton";
                    default: return null;
                }
            }

            return KeyCodeToControlPath(kb.key);
        }

        private static string KeyCodeToControlPath(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z)
                return "<Keyboard>/" + ((char)('a' + (key - KeyCode.A)));

            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return "<Keyboard>/" + ((int)(key - KeyCode.Alpha0));

            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
                return "<Keyboard>/numpad" + ((int)(key - KeyCode.Keypad0));

            if (key >= KeyCode.F1 && key <= KeyCode.F12)
                return "<Keyboard>/f" + (1 + (key - KeyCode.F1));

            switch (key)
            {
                case KeyCode.Escape: return "<Keyboard>/escape";
                case KeyCode.Space: return "<Keyboard>/space";
                case KeyCode.Return: return "<Keyboard>/enter";
                case KeyCode.KeypadEnter: return "<Keyboard>/numpadEnter";
                case KeyCode.Tab: return "<Keyboard>/tab";
                case KeyCode.Backspace: return "<Keyboard>/backspace";
                case KeyCode.Delete: return "<Keyboard>/delete";
                case KeyCode.Insert: return "<Keyboard>/insert";
                case KeyCode.Home: return "<Keyboard>/home";
                case KeyCode.End: return "<Keyboard>/end";
                case KeyCode.PageUp: return "<Keyboard>/pageUp";
                case KeyCode.PageDown: return "<Keyboard>/pageDown";
                case KeyCode.CapsLock: return "<Keyboard>/capsLock";
                case KeyCode.BackQuote: return "<Keyboard>/backquote";
                case KeyCode.Quote: return "<Keyboard>/quote";
                case KeyCode.Semicolon: return "<Keyboard>/semicolon";
                case KeyCode.Comma: return "<Keyboard>/comma";
                case KeyCode.Period: return "<Keyboard>/period";
                case KeyCode.Slash: return "<Keyboard>/slash";
                case KeyCode.Backslash: return "<Keyboard>/backslash";
                case KeyCode.LeftBracket: return "<Keyboard>/leftBracket";
                case KeyCode.RightBracket: return "<Keyboard>/rightBracket";
                case KeyCode.Minus: return "<Keyboard>/minus";
                case KeyCode.Equals: return "<Keyboard>/equals";
                case KeyCode.UpArrow: return "<Keyboard>/upArrow";
                case KeyCode.DownArrow: return "<Keyboard>/downArrow";
                case KeyCode.LeftArrow: return "<Keyboard>/leftArrow";
                case KeyCode.RightArrow: return "<Keyboard>/rightArrow";
                case KeyCode.LeftShift: return "<Keyboard>/leftShift";
                case KeyCode.RightShift: return "<Keyboard>/rightShift";
                case KeyCode.LeftControl: return "<Keyboard>/leftCtrl";
                case KeyCode.RightControl: return "<Keyboard>/rightCtrl";
                case KeyCode.LeftAlt: return "<Keyboard>/leftAlt";
                case KeyCode.RightAlt: return "<Keyboard>/rightAlt";
                case KeyCode.LeftCommand: return "<Keyboard>/leftMeta";
                case KeyCode.RightCommand: return "<Keyboard>/rightMeta";
                default: return null;
            }
        }
    }
}
#endif
