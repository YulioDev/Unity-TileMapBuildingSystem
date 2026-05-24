using System;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    public enum PendingGroupingMode
    {
        IndividualCells,
        CellBlocks
    }

    public enum PendingCostMode
    {
        FixedPerCell,
        DefinitionProvider
    }

    public enum PendingResourcePolicy
    {
        None,
        RequiredBeforeWork,
        AllowPartialDelivery
    }

    [Serializable]
    public sealed class TmbsPendingConstructionConfig
    {
        [Tooltip("How a confirmed drag/click creates pending orders: one per cell or one per block.")]
        public PendingGroupingMode groupingMode = PendingGroupingMode.IndividualCells;

        [Tooltip("Source of cost/work values per cell.")]
        public PendingCostMode costMode = PendingCostMode.FixedPerCell;

        [Tooltip("Whether resource delivery is tracked before work begins.")]
        public PendingResourcePolicy resourcePolicy = PendingResourcePolicy.None;

        [Min(0)]
        [Tooltip("Work units required to complete one cell (FixedPerCell mode).")]
        public int fixedWorkPerCell = 1;

        [Min(0)]
        [Tooltip("Resource units required for one cell (FixedPerCell mode).")]
        public int fixedResourceCostPerCell = 1;

        [Tooltip("If true, placing a pending order over an existing pending cancels the previous one.")]
        public bool replaceExistingPending = false;

        [Tooltip("If true, a block-grouped pending can be partially completed.")]
        public bool allowPartialCompletion = true;

        [Tooltip("If true, AlternateBehaviour (erase) also creates pending orders. Usually false.")]
        public bool createPendingForAlternateBehaviour = false;

        [Tooltip("If true, TMBS emits events when pending orders are created, changed, or completed.")]
        public bool emitPendingEvents = true;
    }
}