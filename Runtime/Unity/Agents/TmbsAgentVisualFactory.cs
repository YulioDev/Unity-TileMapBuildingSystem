using TMBS.Core.Agents;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Agents
{
    internal static class TmbsAgentVisualFactory
    {
        private static Sprite _defaultAgentSprite;
        private static Texture2D _defaultAgentTexture;

        public static GameObject CreateDefaultAgentGameObject(
            string objectName,
            Vector3 worldPosition,
            Transform parent,
            Tilemap referenceTilemap)
        {
            var go = new GameObject(string.IsNullOrEmpty(objectName) ? "TMBS_BuildAgent" : objectName);
            if (parent != null)
                go.transform.SetParent(parent, worldPositionStays: true);

            go.transform.position = worldPosition;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetDefaultSprite();
            sr.color = GetStateColor(TmbsAgentState.Idle);

            if (referenceTilemap != null)
            {
                var tilemapRenderer = referenceTilemap.GetComponent<TilemapRenderer>();
                if (tilemapRenderer != null)
                {
                    sr.sortingLayerID = tilemapRenderer.sortingLayerID;
                    sr.sortingOrder = tilemapRenderer.sortingOrder + 5;
                }
                else
                {
                    sr.sortingOrder = 20;
                }
            }
            else
            {
                sr.sortingOrder = 20;
            }

            return go;
        }

        public static Color GetStateColor(TmbsAgentState state)
        {
            switch (state)
            {
                case TmbsAgentState.MovingToTarget:
                    return new Color(0.25f, 0.85f, 1.0f, 1f);
                case TmbsAgentState.SteppingAside:
                    return new Color(0.75f, 0.45f, 1.0f, 1f);
                case TmbsAgentState.DeliveringResources:
                    return new Color(1.0f, 0.72f, 0.2f, 1f);
                case TmbsAgentState.Building:
                    return new Color(0.3f, 0.95f, 0.42f, 1f);
                case TmbsAgentState.WaitingUnreachable:
                    return new Color(1.0f, 0.3f, 0.3f, 1f);
                case TmbsAgentState.Idle:
                default:
                    return new Color(0.7f, 0.8f, 0.95f, 1f);
            }
        }

        private static Sprite GetDefaultSprite()
        {
            if (_defaultAgentSprite != null)
                return _defaultAgentSprite;

            const int size = 32;
            _defaultAgentTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float center = (size - 1) * 0.5f;
            float outerRadius = 13.5f;
            float innerRadius = 10.0f;
            float coreRadius = 3.5f;

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    Color col;
                    if (dist <= coreRadius)
                    {
                        col = new Color(1f, 1f, 1f, 1f);
                    }
                    else if (dist <= innerRadius)
                    {
                        col = new Color(0.88f, 0.92f, 0.98f, 1f);
                    }
                    else if (dist <= outerRadius)
                    {
                        col = new Color(0.12f, 0.16f, 0.22f, 1f);
                    }
                    else
                    {
                        col = new Color(0f, 0f, 0f, 0f);
                    }

                    pixels[y * size + x] = col;
                }
            }

            _defaultAgentTexture.SetPixels32(pixels);
            _defaultAgentTexture.Apply();

            _defaultAgentSprite = Sprite.Create(
                _defaultAgentTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            _defaultAgentSprite.hideFlags = HideFlags.HideAndDontSave;
            return _defaultAgentSprite;
        }
    }
}
