using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using TMBS.Runtime.Config;
using TMBS.UI;

[CustomEditor(typeof(TmbsBuildableData))]
public class TmbsBuildableDataEditor : Editor
{
    private const float PreviewSize = 128f;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var data = (TmbsBuildableData)target;

        // Draw tile preview at the top
        DrawTilePreview(data);

        EditorGUILayout.Space();

        var isBlueprintProp = serializedObject.FindProperty("isBlueprint");
        var tileProp = serializedObject.FindProperty("tile");

        bool isBlueprint = isBlueprintProp != null && isBlueprintProp.boolValue;
        bool hasTile = tileProp != null && tileProp.objectReferenceValue != null;

        if (isBlueprint && !hasTile)
        {
            EditorGUILayout.HelpBox(
                "Blueprints require a Tile to be assigned.",
                MessageType.Warning);
        }

        if (isBlueprint)
        {
            DrawPropertiesExcluding(serializedObject, "m_Script");
        }
        else
        {
            DrawPropertiesExcluding(serializedObject, "m_Script", "footprintSize");
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTilePreview(TmbsBuildableData data)
    {
        Sprite sprite = TmbsPaletteSlot.ResolveSprite(data, data.tile);

        if (sprite == null)
        {
            EditorGUILayout.HelpBox(
                "No preview available. Assign a Tile with a sprite or set the Preview Sprite field.",
                MessageType.Info);
            return;
        }

        var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(false));
        rect.x = (EditorGUIUtility.currentViewWidth - PreviewSize) * 0.5f;

        // Background box
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));

        // Sprite texture rendering
        var tex = sprite.texture;
        if (tex != null)
        {
            var texRect = sprite.textureRect;
            var uv = new Rect(
                texRect.x / tex.width,
                texRect.y / tex.height,
                texRect.width / tex.width,
                texRect.height / tex.height);

            var padding = 8f;
            var innerRect = new Rect(
                rect.x + padding, rect.y + padding,
                rect.width - padding * 2, rect.height - padding * 2);

            GUI.DrawTextureWithTexCoords(innerRect, tex, uv);
        }

        // Label below preview
        EditorGUILayout.LabelField(
            string.IsNullOrEmpty(data.displayName) ? data.name : data.displayName,
            EditorStyles.centeredGreyMiniLabel);
    }

    public override bool HasPreviewGUI() => true;

    public override void OnPreviewGUI(Rect r, GUIStyle background)
    {
        var data = (TmbsBuildableData)target;
        var sprite = TmbsPaletteSlot.ResolveSprite(data, data.tile);
        if (sprite == null) return;

        var tex = sprite.texture;
        if (tex == null) return;

        var texRect = sprite.textureRect;
        var uv = new Rect(
            texRect.x / tex.width,
            texRect.y / tex.height,
            texRect.width / tex.width,
            texRect.height / tex.height);

        float size = Mathf.Min(r.width, r.height) - 10f;
        var centered = new Rect(
            r.x + (r.width - size) * 0.5f,
            r.y + (r.height - size) * 0.5f,
            size, size);

        GUI.DrawTextureWithTexCoords(centered, tex, uv);
    }
}
