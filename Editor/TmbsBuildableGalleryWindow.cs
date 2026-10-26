using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using TMBS.Runtime.Config;
using TMBS.UI;

public class TmbsBuildableGalleryWindow : EditorWindow
{
    private Vector2 _scrollPos;
    private string _searchFilter = "";
    private string _categoryFilter = "All";
    private readonly List<TmbsBuildableData> _allBuildables = new List<TmbsBuildableData>();
    private readonly List<string> _categories = new List<string> { "All" };
    private float _thumbnailSize = 80f;

    [MenuItem("Window/TMBS/Buildable Gallery")]
    public static void ShowWindow()
    {
        var window = GetWindow<TmbsBuildableGalleryWindow>("TMBS Buildables");
        window.minSize = new Vector2(400, 300);
        window.RefreshDatabase();
    }

    private void OnEnable() => RefreshDatabase();
    private void OnFocus() => RefreshDatabase();

    private void RefreshDatabase()
    {
        _allBuildables.Clear();
        _categories.Clear();
        _categories.Add("All");

        var guids = AssetDatabase.FindAssets("t:TmbsBuildableData");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<TmbsBuildableData>(path);
            if (data != null)
            {
                _allBuildables.Add(data);
                if (!string.IsNullOrEmpty(data.category) && !_categories.Contains(data.category))
                    _categories.Add(data.category);
            }
        }

        _categories.Sort(1, _categories.Count - 1, System.StringComparer.OrdinalIgnoreCase);
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawGallery();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Search
        _searchFilter = EditorGUILayout.TextField(_searchFilter,
            EditorStyles.toolbarSearchField, GUILayout.MinWidth(150));

        // Category filter
        int catIndex = _categories.IndexOf(_categoryFilter);
        if (catIndex < 0) catIndex = 0;
        catIndex = EditorGUILayout.Popup(catIndex, _categories.ToArray(),
            EditorStyles.toolbarPopup, GUILayout.Width(120));
        _categoryFilter = _categories[catIndex];

        // Thumbnail size slider
        GUILayout.FlexibleSpace();
        GUILayout.Label("Size", GUILayout.Width(30));
        _thumbnailSize = GUILayout.HorizontalSlider(_thumbnailSize, 48f, 128f, GUILayout.Width(80));

        // Create new button
        if (GUILayout.Button("+ New", EditorStyles.toolbarButton, GUILayout.Width(50)))
            CreateNewBuildable();

        // Refresh button
        if (GUILayout.Button("↻", EditorStyles.toolbarButton, GUILayout.Width(24)))
            RefreshDatabase();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawGallery()
    {
        var filtered = _allBuildables.Where(b =>
        {
            if (_categoryFilter != "All" && b.category != _categoryFilter)
                return false;
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                var s = _searchFilter.ToLowerInvariant();
                if (!b.displayName.ToLowerInvariant().Contains(s) &&
                    !b.id.ToLowerInvariant().Contains(s))
                    return false;
            }
            return true;
        }).ToList();

        if (filtered.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No buildables found. Click '+ New' to create one.",
                MessageType.Info);
            return;
        }

        float viewWidth = position.width - 20f;
        int columns = Mathf.Max(1, Mathf.FloorToInt(viewWidth / (_thumbnailSize + 10f)));

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        int col = 0;
        EditorGUILayout.BeginHorizontal();

        foreach (var data in filtered)
        {
            if (col >= columns)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                col = 0;
            }

            DrawBuildableCard(data);
            col++;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    private void DrawBuildableCard(TmbsBuildableData data)
    {
        var cardRect = GUILayoutUtility.GetRect(
            _thumbnailSize + 10, _thumbnailSize + 30,
            GUILayout.Width(_thumbnailSize + 10));

        // Card background
        var bgColor = Selection.activeObject == data
            ? new Color(0.24f, 0.48f, 0.9f, 0.3f)
            : new Color(0.2f, 0.2f, 0.2f, 0.5f);
        EditorGUI.DrawRect(cardRect, bgColor);

        // Sprite preview
        var sprite = TmbsPaletteSlot.ResolveSprite(data, data.tile);
        if (sprite != null && sprite.texture != null)
        {
            var tex = sprite.texture;
            var texRect = sprite.textureRect;
            var uv = new Rect(
                texRect.x / tex.width,
                texRect.y / tex.height,
                texRect.width / tex.width,
                texRect.height / tex.height);

            var imgRect = new Rect(
                cardRect.x + 5, cardRect.y + 5,
                _thumbnailSize, _thumbnailSize);

            GUI.DrawTextureWithTexCoords(imgRect, tex, uv);
        }

        // Label
        var labelRect = new Rect(
            cardRect.x, cardRect.y + _thumbnailSize + 5,
            cardRect.width, 20);

        var displayName = string.IsNullOrEmpty(data.displayName) ? data.name : data.displayName;
        if (displayName.Length > 12)
            displayName = displayName.Substring(0, 10) + "..";

        GUI.Label(labelRect, displayName, EditorStyles.centeredGreyMiniLabel);

        // Click to select
        if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.clickCount == 2)
                Selection.activeObject = data;
            else
                EditorGUIUtility.PingObject(data);

            Event.current.Use();
        }
    }

    private void CreateNewBuildable()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Buildable Data",
            "NewBuildable", "asset",
            "Choose where to save the new Buildable Data asset.");

        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<TmbsBuildableData>();
        asset.id = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        asset.displayName = System.IO.Path.GetFileNameWithoutExtension(path);

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        RefreshDatabase();
    }
}
