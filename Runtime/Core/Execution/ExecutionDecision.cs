using TMBS.Core.Validation;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Execution
{
    public enum ExecutionDecisionType
    {
        Reject,
        ExecuteImmediate
    }

    public readonly struct ExecutionDecision
    {
        public readonly ExecutionDecisionType Type;
        public readonly CellMask WriteMask;
        public readonly TileBase WriteTile;
        public readonly TileBase AlternateWriteTile;
        public readonly int ConstructionId;

        public ExecutionDecision(
            ExecutionDecisionType type, 
            CellMask writeMask = null, 
            TileBase writeTile = null, 
            TileBase alternateWriteTile = null, 
            int constructionId = 0)
        {
            Type = type;
            WriteMask = writeMask;
            WriteTile = writeTile;
            AlternateWriteTile = alternateWriteTile;
            ConstructionId = constructionId;
        }

        public static ExecutionDecision Reject => new ExecutionDecision(ExecutionDecisionType.Reject);
    }
}

