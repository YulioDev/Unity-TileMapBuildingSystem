using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using TMBS.Runtime.Config;
using TMBS.Core.Validation;
using System;
using System.Collections.Generic;

[CustomEditor(typeof(TmbsRootConfig))]
public class TmbsRootConfigEditor : Editor
{
    private ReorderableList _list;

    private void OnEnable()
    {
        var prop = serializedObject.FindProperty("validators");

        _list = new ReorderableList(serializedObject, prop, true, true, true, true);

        _list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Validation Pipeline");
        };

        _list.elementHeightCallback = index =>
        {
            var element = prop.GetArrayElementAtIndex(index);
            var validatorProp = element.FindPropertyRelative("validator");
            
            float propertyHeight = EditorGUI.GetPropertyHeight(validatorProp, true);
            return propertyHeight + 4f;
        };

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = prop.GetArrayElementAtIndex(index);

            var enabledProp = element.FindPropertyRelative("enabled");
            var validatorProp = element.FindPropertyRelative("validator");

            string typeName = "Empty";
            if (!string.IsNullOrEmpty(validatorProp.managedReferenceFullTypename))
            {
                var parts = validatorProp.managedReferenceFullTypename.Split(' ');
                typeName = parts.Length > 1 ? parts[1].Substring(parts[1].LastIndexOf('.') + 1) : parts[0];
            }

            rect.y += 2;
            
            var toggleRect = new Rect(rect.x, rect.y, 20, EditorGUIUtility.singleLineHeight);
            
            var propertyHeight = EditorGUI.GetPropertyHeight(validatorProp, true);
            var propRect = new Rect(rect.x + 25, rect.y, rect.width - 25, propertyHeight);

            enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);

            EditorGUI.PropertyField(
                propRect,
                validatorProp,
                new GUIContent(typeName),
                true); 
        };

        _list.onAddDropdownCallback = (rect, list) =>
        {
            var menu = new GenericMenu();

            var validatorTypes = TypeCache.GetTypesDerivedFrom<IValidator>();
            foreach (var type in validatorTypes)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                menu.AddItem(new GUIContent(type.Name), false, (t) =>
                {
                    var targetType = (Type)t;
                    serializedObject.Update();
                    prop.arraySize++;
                    var element = prop.GetArrayElementAtIndex(prop.arraySize - 1);

                    element.FindPropertyRelative("enabled").boolValue = true;
                    element.FindPropertyRelative("validator").managedReferenceValue = Activator.CreateInstance(targetType);

                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssets();
                }, type);
            }

            menu.ShowAsContext();
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            
            if (iterator.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
            else if (iterator.name != "validators"
                     && iterator.name != "input"
                     && iterator.name != "pendingConstruction"
                     && iterator.name != "pendingDebug")
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        EditorGUILayout.Space();

        DrawInputSection();

        EditorGUILayout.Space();

        DrawPendingSections();

        EditorGUILayout.Space();
        
        _list.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPendingSections()
    {
        var executionModeProp = serializedObject.FindProperty("executionMode");
        if (executionModeProp == null) return;

        var executionMode = (ExecutionMode)executionModeProp.intValue;

        if (executionMode != ExecutionMode.Pending)
        {
            EditorGUILayout.HelpBox(
                "Pending Construction and Pending Debug sections are only visible when Execution Mode is set to Pending.",
                MessageType.Info);
            return;
        }

        // Pending Construction
        var pendingProp = serializedObject.FindProperty("pendingConstruction");
        if (pendingProp != null)
        {
            EditorGUILayout.LabelField("Pending Construction", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(pendingProp, true);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        // Pending Debug
        var debugProp = serializedObject.FindProperty("pendingDebug");
        if (debugProp != null)
        {
            EditorGUILayout.LabelField("Pending Debug", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(debugProp, true);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawInputSection()
    {
        var inputProp = serializedObject.FindProperty("input");
        if (inputProp == null)
            return;

        EditorGUILayout.LabelField("Input", EditorStyles.boldLabel);

        var modeProp = inputProp.FindPropertyRelative("mode");
        if (modeProp != null)
            EditorGUILayout.PropertyField(modeProp);

        var mode = (TmbsInputMode)modeProp.intValue;

        switch (mode)
        {
            case TmbsInputMode.Legacy:
                DrawLegacyFields(inputProp);
                break;

            case TmbsInputMode.InputActions:
                DrawInputActionsFields(inputProp);
                break;
        }

        EditorGUILayout.Space(4);

        // Capability validation fields (always visible)
        var strictProp = inputProp.FindPropertyRelative("strictInputValidation");
        if (strictProp != null)
            EditorGUILayout.PropertyField(strictProp);

        DrawCapabilityField(inputProp, "requirePoint");
        DrawCapabilityField(inputProp, "requireConfirm");
        DrawCapabilityField(inputProp, "requireCancel");
        DrawCapabilityField(inputProp, "requireDrag");
        DrawCapabilityField(inputProp, "requireAlternateModifier");
        DrawCapabilityField(inputProp, "allowUndoInput");
        DrawCapabilityField(inputProp, "allowRedoInput");
    }

    private static void DrawCapabilityField(SerializedProperty parent, string fieldName)
    {
        var prop = parent.FindPropertyRelative(fieldName);
        if (prop != null)
            EditorGUILayout.PropertyField(prop);
    }

    private void DrawLegacyFields(SerializedProperty inputProp)
    {
        EditorGUI.indentLevel++;
        var subModeProp = inputProp.FindPropertyRelative("legacySubMode");
        if (subModeProp != null)
        {
            EditorGUILayout.PropertyField(subModeProp, new GUIContent("Device"));
        }
        EditorGUILayout.HelpBox(
            "Legacy: Uses Unity's built-in Input Manager. Currently supports Mouse mode.",
            MessageType.Info);
        EditorGUI.indentLevel--;
    }

    private void DrawInputActionsFields(SerializedProperty inputProp)
    {
        EditorGUI.indentLevel++;
#if ENABLE_INPUT_SYSTEM
        EditorGUILayout.LabelField("Action Bindings", EditorStyles.boldLabel);

        DrawActionField(inputProp, "pointAction", "Pointer Position (Value, Vector2)");
        DrawActionField(inputProp, "dragAction", "Drag / Place (Button)");
        DrawActionField(inputProp, "cancelAction", "Cancel (Button)");
        DrawActionField(inputProp, "undoAction", "Undo (Button)");
        DrawActionField(inputProp, "redoAction", "Redo (Button)");
        DrawActionField(inputProp, "alternateModifierAction", "Alternate Modifier (Button)");

        EditorGUILayout.HelpBox(
            "Assign InputActionReferences from your Input Action Asset.\n" +
            "The 'Drag / Place' action drives the full drag lifecycle (start \u2192 update \u2192 end \u2192 confirm).\n" +
            "The 'Alternate Modifier' (e.g. Shift) toggles erase behaviour.",
            MessageType.Info);
#else
        EditorGUILayout.HelpBox(
            "Input Actions mode requires the Input System package.\n" +
            "Install 'com.unity.inputsystem' via Package Manager to enable this mode.",
            MessageType.Warning);
#endif
        EditorGUI.indentLevel--;
    }

#if ENABLE_INPUT_SYSTEM
    private void DrawActionField(SerializedProperty parent, string fieldName, string label)
    {
        var prop = parent.FindPropertyRelative(fieldName);
        if (prop != null)
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
        }
    }
#endif
}