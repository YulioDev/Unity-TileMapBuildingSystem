using TMBS.Core.Validation;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Execution
{
    public enum ExecutionResult
    {
        ImmediatePlace,
        PendingOrder,
        Reject
    }

    public readonly struct ExecutionDecision
    {
        public readonly ExecutionResult Type;
        public readonly TileBase WriteTile;
        public readonly TileBase AlternateWriteTile;
        public readonly CellMask WriteMask;
        public readonly int ConstructionId;

        public ExecutionDecision(
            ExecutionResult type,
            TileBase writeTile,
            TileBase alternateWriteTile,
            CellMask writeMask = null,
            int constructionId = 0)
        {
            Type = type;
            WriteTile = writeTile;
            AlternateWriteTile = alternateWriteTile;
            WriteMask = writeMask;
            ConstructionId = constructionId;
        }

        public static ExecutionDecision Reject =>
            new ExecutionDecision(ExecutionResult.Reject, null, null, null, 0);

        public static ExecutionDecision Immediate(TileBase tile, CellMask writeMask = null) =>
            new ExecutionDecision(ExecutionResult.ImmediatePlace, tile, null, writeMask, 1);

        public static ExecutionDecision ImmediateWithAlternate(
            TileBase tile,
            TileBase alternateTile,
            CellMask writeMask = null) =>
            new ExecutionDecision(ExecutionResult.ImmediatePlace, tile, alternateTile, writeMask, 1);

        public static ExecutionDecision Pending(TileBase tile, CellMask writeMask = null) =>
            new ExecutionDecision(ExecutionResult.PendingOrder, tile, null, writeMask, 1);

        public ExecutionDecision WithWriteMask(CellMask mask) =>
            new ExecutionDecision(Type, WriteTile, AlternateWriteTile, mask, ConstructionId);
    }
}
