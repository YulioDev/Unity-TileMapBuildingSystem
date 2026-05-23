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

            AddValidatorOption<SelectionValidator>(menu, prop);
            AddValidatorOption<OccupancyValidator>(menu, prop);
            AddValidatorOption<BoundsValidator>(menu, prop);

            menu.ShowAsContext();
        };
    }

    private void AddValidatorOption<T>(GenericMenu menu, SerializedProperty listProp)
        where T : IValidator, new()
    {
        menu.AddItem(new GUIContent(typeof(T).Name), false, () =>
        {
            listProp.arraySize++;
            var element = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);

            element.FindPropertyRelative("enabled").boolValue = true;
            element.FindPropertyRelative("validator").managedReferenceValue = new T();

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        });
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
            else if (iterator.name != "validators")
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        EditorGUILayout.Space();
        
        DrawInputModeHelp();

        EditorGUILayout.Space();
        
        _list.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawInputModeHelp()
    {
        var inputProp = serializedObject.FindProperty("input");
        if (inputProp == null)
            return;

        var modeProp = inputProp.FindPropertyRelative("mode");
        if (modeProp == null)
            return;

        var mode = (TmbsInputMode)modeProp.enumValueIndex;

        switch (mode)
        {
            case TmbsInputMode.Mouse:
                EditorGUILayout.HelpBox(
                    "Mouse: Uses the built-in mouse/keyboard adapter. Works out-of-the-box.",
                    MessageType.Info);
                break;

            case TmbsInputMode.ExternalProvided:
                EditorGUILayout.HelpBox(
                    "ExternalProvided: Requires manual injection via SetExternalInputAdapter before enabling the component.",
                    MessageType.Info);
                break;
        }
    }
}