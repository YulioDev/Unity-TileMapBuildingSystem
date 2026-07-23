using TMBS.Core.Execution;
using TMBS.Core.Validation;
using UnityEngine;

namespace TMBS.Core.Pipeline
{
    public class PipelineContext
    {
        public string InstanceId { get; set; }
        public Vector3 WorldPoint { get; set; }
        public Vector3Int Cell { get; set; }
        public bool HasCell { get; set; }
        public bool AlternateBehaviour { get; set; }
        public UnityEngine.Tilemaps.TileBase SelectedTile { get; set; }
        public bool HasSelection => SelectedTile != null;
        public ValidationResult QuickValidation { get; set; }
        public ValidationResult FullValidation { get; set; }
        public ExecutionDecision Decision { get; set; }
        public BoundsInt DragBounds { get; set; }
        public bool HasDragBounds { get; set; }
        public ValidationFeedback Feedback { get; set; }

        public PipelineContext() { }

        public void Reset(
            string instanceId,
            Vector3 worldPoint,
            Vector3Int cell,
            bool hasCell,
            bool alternateBehaviour,
            UnityEngine.Tilemaps.TileBase selectedTile,
            ValidationResult quickValidation,
            ValidationResult fullValidation,
            ExecutionDecision decision,
            BoundsInt dragBounds,
            bool hasDragBounds,
            ValidationFeedback feedback)
        {
            InstanceId = instanceId;
            WorldPoint = worldPoint;
            Cell = cell;
            HasCell = hasCell;
            AlternateBehaviour = alternateBehaviour;
            SelectedTile = selectedTile;
            QuickValidation = quickValidation;
            FullValidation = fullValidation;
            Decision = decision;
            DragBounds = dragBounds;
            HasDragBounds = hasDragBounds;
            Feedback = feedback;
        }
    }
}

