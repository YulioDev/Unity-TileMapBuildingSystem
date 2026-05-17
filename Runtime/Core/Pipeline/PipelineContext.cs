using TMBS.Core.Execution;
using TMBS.Core.Validation;
using UnityEngine;

namespace TMBS.Core.Pipeline
{
    public readonly struct PipelineContext
    {
        public readonly string InstanceId;
        public readonly Vector3 WorldPoint;
        public readonly Vector3Int Cell;
        public readonly bool HasCell;
        public readonly bool AlternateBehaviour;
        public readonly bool HasSelection;
        public readonly int SelectedBuildableId;
        public readonly ValidationResult QuickValidation;
        public readonly ValidationResult FullValidation;
        public readonly ExecutionDecision Decision;
        public readonly BoundsInt DragBounds;
        public readonly bool HasDragBounds;

        public PipelineContext(
            string instanceId,
            Vector3 worldPoint,
            Vector3Int cell,
            bool hasCell,
            bool alternateBehaviour,
            bool hasSelection,
            int selectedBuildableId,
            ValidationResult quickValidation,
            ValidationResult fullValidation,
            ExecutionDecision decision,
            BoundsInt dragBounds,
            bool hasDragBounds)
        {
            InstanceId = instanceId;
            WorldPoint = worldPoint;
            Cell = cell;
            HasCell = hasCell;
            AlternateBehaviour = alternateBehaviour;
            HasSelection = hasSelection;
            SelectedBuildableId = selectedBuildableId;
            QuickValidation = quickValidation;
            FullValidation = fullValidation;
            Decision = decision;
            DragBounds = dragBounds;
            HasDragBounds = hasDragBounds;
        }

        public PipelineContext WithCell(Vector3Int cell) =>
            new PipelineContext(InstanceId, WorldPoint, cell, true, AlternateBehaviour, HasSelection, SelectedBuildableId, QuickValidation, FullValidation, Decision, DragBounds, HasDragBounds);

        public PipelineContext WithSelection(bool hasSelection, int selectedBuildableId) =>
            new PipelineContext(InstanceId, WorldPoint, Cell, HasCell, AlternateBehaviour, hasSelection, selectedBuildableId, QuickValidation, FullValidation, Decision, DragBounds, HasDragBounds);

        public PipelineContext WithQuickValidation(ValidationResult result) =>
            new PipelineContext(InstanceId, WorldPoint, Cell, HasCell, AlternateBehaviour, HasSelection, SelectedBuildableId, result, FullValidation, Decision, DragBounds, HasDragBounds);

        public PipelineContext WithFullValidation(ValidationResult result) =>
            new PipelineContext(InstanceId, WorldPoint, Cell, HasCell, AlternateBehaviour, HasSelection, SelectedBuildableId, QuickValidation, result, Decision, DragBounds, HasDragBounds);

        public PipelineContext WithDecision(ExecutionDecision decision) =>
            new PipelineContext(InstanceId, WorldPoint, Cell, HasCell, AlternateBehaviour, HasSelection, SelectedBuildableId, QuickValidation, FullValidation, decision, DragBounds, HasDragBounds);

        public PipelineContext WithDragBounds(BoundsInt bounds) =>
            new PipelineContext(InstanceId, WorldPoint, Cell, HasCell, AlternateBehaviour, HasSelection, SelectedBuildableId, QuickValidation, FullValidation, Decision, bounds, true);
    }
}

