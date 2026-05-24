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
    public sealed class TmbsPendingDebugConfig
    {
        [Tooltip("If enabled, pending constructions progress automatically for debugging.")]
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

        [Tooltip("Visual archetype used to render pending/debug states.")]
        public TmbsTileArchetypeConfig tileArchetype = new TmbsTileArchetypeConfig();
    }
}