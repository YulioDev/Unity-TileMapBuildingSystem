using System;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    public enum PendingDebugBuildOrder
    {
        ByStoreOrder,
        ByCellPosition
    }

    [Serializable]
    public sealed class TmbsPendingVisualDebugConfig
    {
        [Tooltip("Master toggle for runtime/editor visual pending overlays (Gizmos & Game View).")]
        public bool enabled = true;

        [Tooltip("Render pending progress bars, labels, and agent overlays inside Game View via OnGUI.")]
        public bool showInGameView = true;

        [Tooltip("Render pending cell outlines, agent paths, and walkability Gizmos inside Scene View.")]
        public bool showInSceneView = true;

        [Tooltip("Draw work and resource progress bars over pending cells.")]
        public bool showProgressBars = true;

        [Tooltip("Draw compact order ID and work/resource counters over pending cells.")]
        public bool showCellLabels = true;

        [Tooltip("Draw state-colored borders around pending cells and golden accents when reserved by an agent.")]
        public bool showStateOutlines = true;

        [Tooltip("Draw lines from active builder agents to their target cells and agent status badges.")]
        public bool showAgentTargets = true;

        [Tooltip("Draw computed pathfinding waypoints for active builder agents.")]
        public bool showAgentPaths = true;

        [Tooltip("Highlight non-walkable cells near pending orders and agents.")]
        public bool showWalkabilityOverlay = false;

        [Min(8f)]
        [Tooltip("Minimum projected cell pixel width required before text labels are drawn in Game View.")]
        public float minCellScreenSizePx = 18f;
    }

    [Serializable]
    public sealed class TmbsPendingDebugConfig
    {
        [Tooltip("Legacy automatic debug builder (no agents). Leave disabled for agent-driven deferred construction.")]
        public bool enabled = false;

        [Min(1)]
        [Tooltip("How much work is added per debug step.")]
        public int workPerStep = 1;

        [Min(1)]
        [Tooltip("How many pending cells are processed per debug step.")]
        public int cellsPerStep = 1;

        [Tooltip("If true, debug mode automatically delivers all required resources before work.")]
        public bool autoDeliverResources = true;

        [Tooltip("Order used by the debug builder when selecting pending cells.")]
        public PendingDebugBuildOrder buildOrder = PendingDebugBuildOrder.ByStoreOrder;

        [Tooltip("If true, pending state is rendered into a debug tilemap.")]
        public bool renderDebugTiles = true;

        [Tooltip("Visual configuration used to render pending/debug states.")]
        public TmbsStateTileConfig tileArchetype = new TmbsStateTileConfig();

        [Tooltip("Visual debug overlays for deferred/pending cells, agent paths, targets, and walkability.")]
        public TmbsPendingVisualDebugConfig visualDebug = new TmbsPendingVisualDebugConfig();

        public TmbsPendingVisualDebugConfig GetVisualDebugConfig()
        {
            if (visualDebug == null)
                visualDebug = new TmbsPendingVisualDebugConfig();
            return visualDebug;
        }
    }
}
