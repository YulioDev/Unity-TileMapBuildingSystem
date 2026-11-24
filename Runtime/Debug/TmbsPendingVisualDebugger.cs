using System.Collections.Generic;
using TMBS.Core.Agents;
using TMBS.Core.Pending;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using TMBS.Unity.Agents;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Runtime.Debugging
{
    /// <summary>
    /// Visual debug renderer for deferred/pending constructions, builder agent targets,
    /// planned paths, and grid walkability across both Scene View (Gizmos) and Game View (OnGUI).
    /// </summary>
    [AddComponentMenu("TMBS/Pending Visual Debugger")]
    [DisallowMultipleComponent]
    public sealed class TmbsPendingVisualDebugger : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private BuildeableTilemap buildeableTilemap;
        [SerializeField] private TmbsBuildAgentManager agentManager;

        [Header("Fallback Visual Settings (used when RootConfig is missing)")]
        [SerializeField] private TmbsPendingVisualDebugConfig fallbackVisualConfig = new TmbsPendingVisualDebugConfig();

        private const int MaxWalkabilityCellsPerFrame = 256;

        private Texture2D _whitePixel;
        private GUIStyle _cellLabelStyle;
        private GUIStyle _agentBadgeStyle;
        private readonly List<TmbsBuildAgent> _fallbackAgents = new List<TmbsBuildAgent>(16);
        private readonly HashSet<Vector3Int> _walkabilitySampleCells = new HashSet<Vector3Int>();
        private TmbsTilemapWalkability _fallbackWalkability;

        public BuildeableTilemap TargetBuildeableTilemap
        {
            get => buildeableTilemap;
            set => buildeableTilemap = value;
        }

        public TmbsBuildAgentManager AgentManager
        {
            get => agentManager;
            set => agentManager = value;
        }

        public TmbsPendingVisualDebugConfig ActiveConfig
        {
            get
            {
                if (buildeableTilemap != null && buildeableTilemap.RootConfig != null)
                    return buildeableTilemap.RootConfig.GetPendingDebugConfig().GetVisualDebugConfig();

                if (fallbackVisualConfig == null)
                    fallbackVisualConfig = new TmbsPendingVisualDebugConfig();
                return fallbackVisualConfig;
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDestroy()
        {
            if (_whitePixel != null)
            {
                if (Application.isPlaying)
                    Destroy(_whitePixel);
                else
                    DestroyImmediate(_whitePixel);
                _whitePixel = null;
            }
        }

        private void OnGUI()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            var cfg = ActiveConfig;
            if (cfg == null || !cfg.enabled || !cfg.showInGameView)
                return;

            ResolveReferences();
            if (buildeableTilemap == null || buildeableTilemap.TargetTilemap == null)
                return;

            Camera cam = buildeableTilemap.ResolvedCamera ?? Camera.main;
            if (cam == null)
                return;

            EnsureGuiResources();

            Tilemap tilemap = buildeableTilemap.TargetTilemap;
            var workApi = buildeableTilemap.PendingWorkApi;
            IReadOnlyList<PendingOrder> orders = workApi != null ? workApi.GetAll() : null;
            IReadOnlyList<TmbsBuildAgent> agents = CollectActiveAgents();
            ResourceRequirement policy = GetResourcePolicy();

            if (cfg.showWalkabilityOverlay)
            {
                DrawWalkabilityGameView(cam, tilemap, orders, agents);
            }

            if (orders != null && (cfg.showStateOutlines || cfg.showProgressBars || cfg.showCellLabels))
            {
                DrawPendingCellsGameView(cam, tilemap, orders, policy, cfg);
            }

            if (agents != null && agents.Count > 0 && (cfg.showAgentPaths || cfg.showAgentTargets))
            {
                DrawAgentsGameView(cam, tilemap, agents, cfg);
            }
        }

        private void OnDrawGizmos()
        {
            var cfg = ActiveConfig;
            if (cfg == null || !cfg.enabled || !cfg.showInSceneView)
                return;

            ResolveReferences();
            if (buildeableTilemap == null || buildeableTilemap.TargetTilemap == null)
                return;

            Tilemap tilemap = buildeableTilemap.TargetTilemap;
            Vector3 cellSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : Vector3.one;
            Vector3 cubeSize = new Vector3(cellSize.x * 0.92f, cellSize.y * 0.92f, 0.05f);

            var workApi = buildeableTilemap.PendingWorkApi;
            IReadOnlyList<PendingOrder> orders = workApi != null ? workApi.GetAll() : null;
            IReadOnlyList<TmbsBuildAgent> agents = CollectActiveAgents();
            ResourceRequirement policy = GetResourcePolicy();

            if (cfg.showWalkabilityOverlay)
            {
                var walkability = ResolveWalkabilityProvider();
                CollectWalkabilitySampleCells(tilemap, orders, agents, _walkabilitySampleCells);
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.65f);
                foreach (var cell in _walkabilitySampleCells)
                {
                    if (walkability != null && !walkability.IsWalkable(cell))
                    {
                        Vector3 center = tilemap.GetCellCenterWorld(cell);
                        Gizmos.DrawWireCube(center, cubeSize * 0.85f);
                    }
                }
            }

            if (cfg.showStateOutlines && orders != null)
            {
                for (int o = 0; o < orders.Count; o++)
                {
                    var order = orders[o];
                    if (order == null || order.Cells == null)
                        continue;

                    for (int c = 0; c < order.Cells.Length; c++)
                    {
                        var cell = order.Cells[c];
                        if (cell.IsCompleted)
                            continue;

                        Gizmos.color = ResolvePendingCellColor(in cell, policy);
                        Vector3 center = tilemap.GetCellCenterWorld(cell.Cell);
                        Gizmos.DrawWireCube(center, cubeSize);
                    }
                }
            }

            if (agents != null)
            {
                for (int i = 0; i < agents.Count; i++)
                {
                    var agent = agents[i];
                    if (agent == null || !agent.HasActiveJob)
                        continue;

                    if (cfg.showAgentPaths && agent.CurrentPath != null && agent.CurrentPath.Count > 0)
                    {
                        Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.85f);
                        Vector3 prev = agent.WorldPosition;
                        int startIdx = Mathf.Clamp(agent.CurrentPathIndex, 0, agent.CurrentPath.Count - 1);
                        for (int p = startIdx; p < agent.CurrentPath.Count; p++)
                        {
                            Vector3 wp = tilemap.GetCellCenterWorld(agent.CurrentPath[p]);
                            Gizmos.DrawLine(prev, wp);
                            Gizmos.DrawSphere(wp, 0.08f);
                            prev = wp;
                        }
                    }

                    if (cfg.showAgentTargets)
                    {
                        Vector3 targetCenter = tilemap.GetCellCenterWorld(agent.CurrentJob.TargetCell);
                        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                        Gizmos.DrawLine(agent.WorldPosition, targetCenter);
                        Gizmos.DrawWireCube(targetCenter, cubeSize * 0.6f);
                    }
                }
            }
        }

        private void DrawWalkabilityGameView(
            Camera cam,
            Tilemap tilemap,
            IReadOnlyList<PendingOrder> orders,
            IReadOnlyList<TmbsBuildAgent> agents)
        {
            var walkability = ResolveWalkabilityProvider();
            if (walkability == null)
                return;

            CollectWalkabilitySampleCells(tilemap, orders, agents, _walkabilitySampleCells);
            Color blockedBorder = new Color(1f, 0.25f, 0.25f, 0.72f);
            Color blockedFill = new Color(0.95f, 0.15f, 0.15f, 0.14f);

            foreach (var cell in _walkabilitySampleCells)
            {
                if (walkability.IsWalkable(cell))
                    continue;

                if (!TryGetCellGuiRect(cam, tilemap, cell, out Rect r))
                    continue;

                DrawSolidRect(r, blockedFill);
                DrawRectOutline(r, 1.5f, blockedBorder);
                DrawScreenLine(new Vector2(r.xMin + 2f, r.yMin + 2f), new Vector2(r.xMax - 2f, r.yMax - 2f), 1.25f, blockedBorder);
                DrawScreenLine(new Vector2(r.xMax - 2f, r.yMin + 2f), new Vector2(r.xMin + 2f, r.yMax - 2f), 1.25f, blockedBorder);
            }
        }

        private void DrawPendingCellsGameView(
            Camera cam,
            Tilemap tilemap,
            IReadOnlyList<PendingOrder> orders,
            ResourceRequirement policy,
            TmbsPendingVisualDebugConfig cfg)
        {
            for (int o = 0; o < orders.Count; o++)
            {
                var order = orders[o];
                if (order == null || order.Cells == null)
                    continue;

                for (int c = 0; c < order.Cells.Length; c++)
                {
                    var cell = order.Cells[c];
                    if (cell.IsCompleted)
                        continue;

                    if (!TryGetCellGuiRect(cam, tilemap, cell.Cell, out Rect r))
                        continue;

                    Color stateColor = ResolvePendingCellColor(in cell, policy);
                    bool isReserved = agentManager != null && agentManager.IsCellReserved(cell.Cell);

                    if (cfg.showStateOutlines)
                    {
                        DrawRectOutline(r, 2f, stateColor);
                        if (isReserved && r.width > 8f && r.height > 8f)
                        {
                            Rect inner = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f);
                            DrawRectOutline(inner, 1.5f, new Color(1f, 0.9f, 0.25f, 0.95f));
                        }
                    }

                    if (cfg.showProgressBars && r.width >= 14f)
                    {
                        float barWidth = Mathf.Max(12f, r.width - 6f);
                        float barX = r.x + (r.width - barWidth) * 0.5f;
                        float bottomY = r.yMax - 7f;

                        float workFrac = cell.WorkRequired > 0
                            ? Mathf.Clamp01((float)cell.WorkDone / cell.WorkRequired)
                            : 1f;
                        DrawMiniBar(new Rect(barX, bottomY, barWidth, 5f), workFrac, Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(0.2f, 0.95f, 0.4f), workFrac));

                        if (cell.ResourceRequired > 0)
                        {
                            float resFrac = Mathf.Clamp01((float)cell.ResourceDelivered / cell.ResourceRequired);
                            DrawMiniBar(new Rect(barX, bottomY - 6f, barWidth, 4f), resFrac, new Color(1f, 0.8f, 0.2f, 1f));
                        }
                    }

                    if (cfg.showCellLabels && r.width >= cfg.minCellScreenSizePx)
                    {
                        string text = cell.Tile == null
                            ? $"#{order.Id} DEL {cell.WorkDone}/{cell.WorkRequired}"
                            : (cell.ResourceRequired > 0
                                ? $"#{order.Id} W:{cell.WorkDone}/{cell.WorkRequired} R:{cell.ResourceDelivered}/{cell.ResourceRequired}"
                                : $"#{order.Id} W:{cell.WorkDone}/{cell.WorkRequired}");

                        Rect pill = new Rect(r.x, Mathf.Max(2f, r.y - 16f), Mathf.Max(r.width, 64f), 15f);
                        DrawSolidRect(pill, new Color(0.06f, 0.08f, 0.12f, 0.78f));
                        GUI.Label(pill, text, _cellLabelStyle);
                    }
                }
            }
        }

        private void DrawAgentsGameView(
            Camera cam,
            Tilemap tilemap,
            IReadOnlyList<TmbsBuildAgent> agents,
            TmbsPendingVisualDebugConfig cfg)
        {
            Color pathColor = new Color(0.25f, 0.9f, 1f, 0.85f);
            Color targetLineColor = new Color(1f, 0.85f, 0.2f, 0.9f);

            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent == null || !agent.isActiveAndEnabled)
                    continue;

                if (cfg.showAgentPaths && agent.HasActiveJob && agent.CurrentPath != null && agent.CurrentPath.Count > 0)
                {
                    if (TryWorldToGui(cam, agent.WorldPosition, out Vector2 prevGui))
                    {
                        int startIdx = Mathf.Clamp(agent.CurrentPathIndex, 0, agent.CurrentPath.Count - 1);
                        for (int p = startIdx; p < agent.CurrentPath.Count; p++)
                        {
                            Vector3 wp = tilemap.GetCellCenterWorld(agent.CurrentPath[p]);
                            if (!TryWorldToGui(cam, wp, out Vector2 nextGui))
                                continue;

                            DrawScreenLine(prevGui, nextGui, 2f, pathColor);
                            DrawSolidRect(new Rect(nextGui.x - 2.5f, nextGui.y - 2.5f, 5f, 5f), pathColor);
                            prevGui = nextGui;
                        }
                    }
                }

                if (cfg.showAgentTargets)
                {
                    if (TryWorldToGui(cam, agent.WorldPosition, out Vector2 agentGui))
                    {
                        if (agent.HasActiveJob)
                        {
                            Vector3 targetWorld = tilemap.GetCellCenterWorld(agent.CurrentJob.TargetCell);
                            if (TryWorldToGui(cam, targetWorld, out Vector2 targetGui))
                            {
                                DrawScreenLine(agentGui, targetGui, 2f, targetLineColor);
                                DrawRectOutline(new Rect(targetGui.x - 5f, targetGui.y - 5f, 10f, 10f), 2f, targetLineColor);
                            }
                        }

                        string badge = agent.HasActiveJob
                            ? $"{agent.gameObject.name} [{agent.State}]"
                            : $"{agent.gameObject.name} [Idle]";
                        Rect badgeRect = new Rect(agentGui.x - 65f, agentGui.y - 28f, 130f, 16f);
                        DrawSolidRect(badgeRect, new Color(0.08f, 0.1f, 0.14f, 0.82f));
                        GUI.Label(badgeRect, badge, _agentBadgeStyle);
                    }
                }
            }
        }

        private void DrawMiniBar(Rect rect, float progress, Color fillColor)
        {
            DrawSolidRect(rect, new Color(0.05f, 0.06f, 0.08f, 0.88f));
            if (progress > 0.001f)
            {
                Rect fill = new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(1f, (rect.width - 2f) * Mathf.Clamp01(progress)), Mathf.Max(1f, rect.height - 2f));
                DrawSolidRect(fill, fillColor);
            }
        }

        private void DrawSolidRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _whitePixel);
            GUI.color = prev;
        }

        private void DrawRectOutline(Rect rect, float thickness, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), _whitePixel);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), _whitePixel);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), _whitePixel);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), _whitePixel);
            GUI.color = prev;
        }

        private void DrawScreenLine(Vector2 pointA, Vector2 pointB, float thickness, Color color)
        {
            Vector2 delta = pointB - pointA;
            float length = delta.magnitude;
            if (length < 0.5f)
                return;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;

            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, pointA);
            GUI.DrawTexture(new Rect(pointA.x, pointA.y - thickness * 0.5f, length, thickness), _whitePixel);

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        private static bool TryWorldToGui(Camera cam, Vector3 worldPos, out Vector2 guiPos)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            if (sp.z < 0f)
            {
                guiPos = default;
                return false;
            }

            guiPos = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }

        private static bool TryGetCellGuiRect(Camera cam, Tilemap tilemap, Vector3Int cell, out Rect rect)
        {
            Vector3 worldMin = tilemap.CellToWorld(cell);
            Vector3 cellSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : Vector3.one;
            Vector3 worldMax = worldMin + new Vector3(cellSize.x, cellSize.y, 0f);

            if (!TryWorldToGui(cam, worldMin, out Vector2 p0) ||
                !TryWorldToGui(cam, worldMax, out Vector2 p1))
            {
                rect = default;
                return false;
            }

            float xMin = Mathf.Min(p0.x, p1.x);
            float yMin = Mathf.Min(p0.y, p1.y);
            float w = Mathf.Abs(p1.x - p0.x);
            float h = Mathf.Abs(p1.y - p0.y);

            if (w < 2f || h < 2f ||
                xMin > Screen.width || xMin + w < 0f ||
                yMin > Screen.height || yMin + h < 0f)
            {
                rect = default;
                return false;
            }

            rect = new Rect(xMin, yMin, w, h);
            return true;
        }

        private static Color ResolvePendingCellColor(in PendingCell cell, ResourceRequirement policy)
        {
            if (cell.Tile == null)
                return new Color(1f, 0.32f, 0.32f, 0.95f);

            switch (cell.GetCellVisualState(policy))
            {
                case PendingState.WaitingForResources:
                    return new Color(1f, 0.7f, 0.18f, 0.95f);
                case PendingState.InProgress:
                    return new Color(0.28f, 0.85f, 1f, 0.95f);
                case PendingState.ReadyToBuild:
                default:
                    return new Color(0.32f, 0.95f, 0.45f, 0.95f);
            }
        }

        private void CollectWalkabilitySampleCells(
            Tilemap tilemap,
            IReadOnlyList<PendingOrder> orders,
            IReadOnlyList<TmbsBuildAgent> agents,
            HashSet<Vector3Int> output)
        {
            output.Clear();
            const int radius = 3;

            if (orders != null)
            {
                for (int o = 0; o < orders.Count && output.Count < MaxWalkabilityCellsPerFrame; o++)
                {
                    var order = orders[o];
                    if (order == null || order.Cells == null)
                        continue;

                    for (int c = 0; c < order.Cells.Length && output.Count < MaxWalkabilityCellsPerFrame; c++)
                    {
                        AddNeighborhood(order.Cells[c].Cell, radius, output);
                    }
                }
            }

            if (agents != null)
            {
                for (int i = 0; i < agents.Count && output.Count < MaxWalkabilityCellsPerFrame; i++)
                {
                    if (agents[i] == null)
                        continue;

                    Vector3Int cell = tilemap.WorldToCell(agents[i].WorldPosition);
                    AddNeighborhood(cell, radius, output);
                }
            }
        }

        private static void AddNeighborhood(Vector3Int center, int radius, HashSet<Vector3Int> output)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (output.Count >= MaxWalkabilityCellsPerFrame)
                        return;
                    output.Add(new Vector3Int(center.x + dx, center.y + dy, center.z));
                }
            }
        }

        private ITmbsGridWalkabilityProvider ResolveWalkabilityProvider()
        {
            if (agentManager != null)
                return agentManager.WalkabilityProvider;

            if (buildeableTilemap == null)
                return null;

            if (_fallbackWalkability == null)
                _fallbackWalkability = new TmbsTilemapWalkability(buildeableTilemap.Metadata, buildeableTilemap.TargetTilemap);
            else
                _fallbackWalkability.SetSources(buildeableTilemap.Metadata, buildeableTilemap.TargetTilemap);

            return _fallbackWalkability;
        }

        private IReadOnlyList<TmbsBuildAgent> CollectActiveAgents()
        {
            if (agentManager != null)
                return agentManager.ActiveAgents;

            _fallbackAgents.Clear();
#if UNITY_6000_0_OR_NEWER
            var found = FindObjectsByType<TmbsBuildAgent>();
#elif UNITY_2023_1_OR_NEWER
            var found = FindObjectsByType<TmbsBuildAgent>(FindObjectsSortMode.None);
#else
            var found = FindObjectsOfType<TmbsBuildAgent>();
#endif
            if (found != null)
            {
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null)
                        _fallbackAgents.Add(found[i]);
                }
            }

            return _fallbackAgents;
        }

        private ResourceRequirement GetResourcePolicy()
        {
            if (buildeableTilemap != null && buildeableTilemap.RootConfig != null)
                return buildeableTilemap.RootConfig.GetPendingConfig().resourcePolicy;
            return ResourceRequirement.None;
        }

        private void ResolveReferences()
        {
            if (buildeableTilemap == null)
            {
                buildeableTilemap = GetComponent<BuildeableTilemap>();
                if (buildeableTilemap == null)
                {
#if UNITY_2023_1_OR_NEWER
                    buildeableTilemap = FindAnyObjectByType<BuildeableTilemap>();
#else
                    buildeableTilemap = FindObjectOfType<BuildeableTilemap>();
#endif
                }
            }

            if (agentManager == null)
            {
                agentManager = GetComponent<TmbsBuildAgentManager>();
                if (agentManager == null)
                {
#if UNITY_2023_1_OR_NEWER
                    agentManager = FindAnyObjectByType<TmbsBuildAgentManager>();
#else
                    agentManager = FindObjectOfType<TmbsBuildAgentManager>();
#endif
                }
            }
        }

        private void EnsureGuiResources()
        {
            if (_whitePixel == null)
            {
                _whitePixel = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _whitePixel.SetPixel(0, 0, Color.white);
                _whitePixel.Apply();
            }

            if (_cellLabelStyle == null)
            {
                _cellLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(2, 2, 0, 0),
                    normal = { textColor = Color.white }
                };
            }

            if (_agentBadgeStyle == null)
            {
                _agentBadgeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.75f, 0.95f, 1f, 1f) }
                };
            }
        }
    }
}
