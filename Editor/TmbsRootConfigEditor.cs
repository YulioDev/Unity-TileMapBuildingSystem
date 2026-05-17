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
            
            // La altura dinámica de la propiedad según esté plegada (foldout) o desplegada, más algo de padding.
            float propertyHeight = EditorGUI.GetPropertyHeight(validatorProp, true);
            return propertyHeight + 4f;
        };

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = prop.GetArrayElementAtIndex(index);

            var enabledProp = element.FindPropertyRelative("enabled");
            var validatorProp = element.FindPropertyRelative("validator");

            // Extraer el nombre de la clase instanciada para ponerla como título del foldout
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
                true); // includeChildren = true
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

        // Iteramos manualmente las propiedades para NO dibujar 'validators' duplicado
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
        
        // Dibujamos nuestra ReorderableList custom
        _list.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }
}