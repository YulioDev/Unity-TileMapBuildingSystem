using System;
using System.Collections.Generic;
using TMBS.Core.Pending;
using TMBS.Runtime.Config;
using UnityEngine;

namespace TMBS.Core.Agents
{
    public sealed class TmbsSmartJobPlanner
    {
        private struct CandidateJob
        {
            public int OrderId;
            public Vector3Int TargetCell;
            public bool IsDemolition;
            public bool BlocksMovement;
            public int StandStartIndex;
            public int StandCount;
            public int BaseScore;
            public int PreScore;
            public int InsertionIndex;
        }

        private const int MaxEvaluatedCandidates = 24;
        private const int SealPendingNeighborPenalty = 100000;

        private static readonly Vector3Int[] CardinalOffsets =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0)
        };

        private readonly HashSet<Vector3Int> _pendingBlockingCells = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> _allPendingCells = new HashSet<Vector3Int>();
        private readonly List<CandidateJob> _candidates = new List<CandidateJob>(64);
        private readonly List<Vector3Int> _allStandCells = new List<Vector3Int>(256);
        private readonly List<Vector3Int> _standSlice = new List<Vector3Int>(16);
        private readonly List<Vector3Int> _scratchStand = new List<Vector3Int>(16);
        private readonly List<Vector3Int> _tempPath = new List<Vector3Int>(64);

        public bool TryPlanBestJob(
            Vector3Int agentCell,
            IReadOnlyList<PendingOrder> orders,
            ResourceRequirement resourcePolicy,
            bool canDeliverResources,
            bool allowPartialCompletion,
            bool treatUnrecordedTilesAsBlocking,
            float buildRangeCells,
            bool usePathfinding,
            bool allowDiagonal,
            TmbsAgentTargetStrategy strategy,
            ITmbsGridWalkabilityProvider walkability,
            ITmbsGridPathfinder pathfinder,
            Func<Vector3Int, bool> isCellReserved,
            List<Vector3Int> pathBuffer,
            out TmbsAgentJob job,
            int maxExpandedNodes = 2048)
        {
            if (pathBuffer == null)
                throw new ArgumentNullException(nameof(pathBuffer));

            pathBuffer.Clear();
            job = default;

            if (orders == null || orders.Count == 0)
                return false;

            BuildPendingSets(orders, treatUnrecordedTilesAsBlocking);
            _candidates.Clear();
            _allStandCells.Clear();

            bool allowDiagonalReach = allowDiagonal || buildRangeCells >= 1.41f;

            for (int o = 0; o < orders.Count; o++)
            {
                var order = orders[o];
                if (order == null || order.Cells == null)
                    continue;

                bool allWorkCompleteInOrder = AreAllCellsWorkComplete(order);

                for (int c = 0; c < order.Cells.Length; c++)
                {
                    var cellData = order.Cells[c];
                    if (cellData.IsCompleted)
                        continue;

                    if (isCellReserved != null && isCellReserved(cellData.Cell))
                        continue;

                    if (!allowPartialCompletion && order.Cells.Length > 1 && cellData.IsWorkComplete && !allWorkCompleteInOrder)
                        continue;

                    if (!canDeliverResources && !cellData.IsWorkComplete && cellData.WorkAllowed(resourcePolicy) <= cellData.WorkDone)
                        continue;

                    bool isDemolition = cellData.Tile == null;
                    bool blocksMovement = DoesCellBlockMovement(in cellData, treatUnrecordedTilesAsBlocking);

                    TmbsGridPathfinder.CollectValidStandCells(
                        cellData.Cell,
                        buildRangeCells,
                        allowStandingOnTarget: !blocksMovement,
                        allowDiagonalReach: allowDiagonalReach,
                        walkability: walkability,
                        results: _scratchStand);

                    if (_scratchStand.Count == 0)
                        continue;

                    int startIdx = _allStandCells.Count;
                    for (int s = 0; s < _scratchStand.Count; s++)
                        _allStandCells.Add(_scratchStand[s]);

                    int manhattan = Math.Abs(agentCell.x - cellData.Cell.x) + Math.Abs(agentCell.y - cellData.Cell.y);
                    int baseScore = ComputeBaseScore(
                        in cellData,
                        blocksMovement,
                        strategy,
                        walkability,
                        _candidates.Count);
                    int preScore = CombineDistanceScore(strategy, baseScore, manhattan);

                    _candidates.Add(new CandidateJob
                    {
                        OrderId = order.Id,
                        TargetCell = cellData.Cell,
                        IsDemolition = isDemolition,
                        BlocksMovement = blocksMovement,
                        StandStartIndex = startIdx,
                        StandCount = _scratchStand.Count,
                        BaseScore = baseScore,
                        PreScore = preScore,
                        InsertionIndex = _candidates.Count
                    });
                }
            }

            if (_candidates.Count == 0)
                return false;

            if (strategy != TmbsAgentTargetStrategy.OrderQueue)
            {
                _candidates.Sort((a, b) =>
                {
                    int cmp = a.PreScore.CompareTo(b.PreScore);
                    return cmp != 0 ? cmp : a.InsertionIndex.CompareTo(b.InsertionIndex);
                });
            }

            if (!usePathfinding || pathfinder == null)
            {
                var chosen = _candidates[0];
                Vector3Int bestStand = PickClosestStandCell(agentCell, chosen.StandStartIndex, chosen.StandCount);
                if (agentCell != bestStand)
                    pathBuffer.Add(agentCell);
                pathBuffer.Add(bestStand);
                job = new TmbsAgentJob(chosen.OrderId, chosen.TargetCell, bestStand, chosen.IsDemolition, chosen.BlocksMovement);
                return true;
            }

            bool foundAny = false;
            int bestFinalScore = int.MaxValue;
            int bestInsertion = int.MaxValue;
            CandidateJob bestCandidate = default;
            Vector3Int bestReachedStand = default;

            int evaluatedReachable = 0;

            for (int i = 0; i < _candidates.Count; i++)
            {
                var cand = _candidates[i];

                if (foundAny && strategy == TmbsAgentTargetStrategy.OrderQueue)
                    break;

                if (foundAny && evaluatedReachable >= MaxEvaluatedCandidates)
                    break;

                if (foundAny && cand.BaseScore >= bestFinalScore)
                    continue;

                PopulateStandSlice(cand.StandStartIndex, cand.StandCount);

                if (!pathfinder.TryFindPathToAnyGoal(
                    agentCell,
                    _standSlice,
                    walkability,
                    _tempPath,
                    out var reachedStand,
                    allowDiagonal,
                    maxExpandedNodes))
                {
                    continue;
                }

                evaluatedReachable++;
                int pathSteps = Math.Max(0, _tempPath.Count - 1);
                int finalScore = CombineDistanceScore(strategy, cand.BaseScore, pathSteps);

                if (!foundAny ||
                    finalScore < bestFinalScore ||
                    (finalScore == bestFinalScore && cand.InsertionIndex < bestInsertion))
                {
                    foundAny = true;
                    bestFinalScore = finalScore;
                    bestInsertion = cand.InsertionIndex;
                    bestCandidate = cand;
                    bestReachedStand = reachedStand;

                    pathBuffer.Clear();
                    for (int p = 0; p < _tempPath.Count; p++)
                        pathBuffer.Add(_tempPath[p]);
                }
            }

            if (!foundAny)
                return false;

            job = new TmbsAgentJob(
                bestCandidate.OrderId,
                bestCandidate.TargetCell,
                bestReachedStand,
                bestCandidate.IsDemolition,
                bestCandidate.BlocksMovement);
            return true;
        }

        public bool TryPlanSpecificCell(
            Vector3Int agentCell,
            Vector3Int targetCell,
            int orderId,
            in PendingCell cellData,
            bool treatUnrecordedTilesAsBlocking,
            float buildRangeCells,
            bool usePathfinding,
            bool allowDiagonal,
            ITmbsGridWalkabilityProvider walkability,
            ITmbsGridPathfinder pathfinder,
            List<Vector3Int> pathBuffer,
            out TmbsAgentJob job,
            int maxExpandedNodes = 2048)
        {
            if (pathBuffer == null)
                throw new ArgumentNullException(nameof(pathBuffer));

            pathBuffer.Clear();
            job = default;

            if (cellData.IsCompleted)
                return false;

            bool isDemolition = cellData.Tile == null;
            bool blocksMovement = DoesCellBlockMovement(in cellData, treatUnrecordedTilesAsBlocking);
            bool allowDiagonalReach = allowDiagonal || buildRangeCells >= 1.41f;

            TmbsGridPathfinder.CollectValidStandCells(
                targetCell,
                buildRangeCells,
                allowStandingOnTarget: !blocksMovement,
                allowDiagonalReach: allowDiagonalReach,
                walkability: walkability,
                results: _scratchStand);

            if (_scratchStand.Count == 0)
                return false;

            if (!usePathfinding || pathfinder == null)
            {
                Vector3Int stand = PickClosestStandCellFromScratch(agentCell);
                if (agentCell != stand)
                    pathBuffer.Add(agentCell);
                pathBuffer.Add(stand);
                job = new TmbsAgentJob(orderId, targetCell, stand, isDemolition, blocksMovement);
                return true;
            }

            if (!pathfinder.TryFindPathToAnyGoal(
                agentCell,
                _scratchStand,
                walkability,
                pathBuffer,
                out var reachedStand,
                allowDiagonal,
                maxExpandedNodes))
            {
                return false;
            }

            job = new TmbsAgentJob(orderId, targetCell, reachedStand, isDemolition, blocksMovement);
            return true;
        }

        private void BuildPendingSets(IReadOnlyList<PendingOrder> orders, bool treatUnrecordedTilesAsBlocking)
        {
            _pendingBlockingCells.Clear();
            _allPendingCells.Clear();

            for (int o = 0; o < orders.Count; o++)
            {
                var order = orders[o];
                if (order == null || order.Cells == null)
                    continue;

                for (int c = 0; c < order.Cells.Length; c++)
                {
                    var cellData = order.Cells[c];
                    if (cellData.IsCompleted)
                        continue;

                    _allPendingCells.Add(cellData.Cell);
                    if (DoesCellBlockMovement(in cellData, treatUnrecordedTilesAsBlocking))
                        _pendingBlockingCells.Add(cellData.Cell);
                }
            }
        }

        private int ComputeBaseScore(
            in PendingCell cellData,
            bool blocksMovement,
            TmbsAgentTargetStrategy strategy,
            ITmbsGridWalkabilityProvider walkability,
            int insertionIndex)
        {
            if (strategy == TmbsAgentTargetStrategy.OrderQueue)
                return insertionIndex;

            if (strategy == TmbsAgentTargetStrategy.Nearest)
                return cellData.WorkDone > 0 ? -5 : 0;

            int futureOpenNeighbors = 0;
            int currentWalkableNeighbors = 0;
            bool wouldSealPendingNeighbor = false;

            for (int i = 0; i < CardinalOffsets.Length; i++)
            {
                Vector3Int neighbor = cellData.Cell + CardinalOffsets[i];
                bool walkableNow = walkability == null || walkability.IsWalkable(neighbor);

                if (walkableNow)
                {
                    currentWalkableNeighbors++;
                    if (!_pendingBlockingCells.Contains(neighbor))
                        futureOpenNeighbors++;
                }

                if (blocksMovement && _allPendingCells.Contains(neighbor))
                {
                    // Check whether building this cell now would strip the neighbor pending cell
                    // of its last walkable cardinal access point.
                    int neighborOtherOpen = 0;
                    for (int j = 0; j < CardinalOffsets.Length; j++)
                    {
                        Vector3Int n2 = neighbor + CardinalOffsets[j];
                        if (n2 == cellData.Cell)
                            continue;

                        if (walkability == null || walkability.IsWalkable(n2))
                            neighborOtherOpen++;
                    }

                    if (neighborOtherOpen == 0)
                        wouldSealPendingNeighbor = true;
                }
            }

            int score = futureOpenNeighbors * 100 + currentWalkableNeighbors * 10;
            if (wouldSealPendingNeighbor)
                score += SealPendingNeighborPenalty;
            if (cellData.WorkDone > 0)
                score -= 25;

            return score;
        }

        private static int CombineDistanceScore(TmbsAgentTargetStrategy strategy, int baseScore, int distanceSteps)
        {
            switch (strategy)
            {
                case TmbsAgentTargetStrategy.SmartReachable:
                    return baseScore + distanceSteps * 4;
                case TmbsAgentTargetStrategy.Nearest:
                    return distanceSteps * 10 + baseScore;
                default:
                    return baseScore;
            }
        }

        private void PopulateStandSlice(int startIdx, int count)
        {
            _standSlice.Clear();
            for (int i = 0; i < count; i++)
                _standSlice.Add(_allStandCells[startIdx + i]);
        }

        private Vector3Int PickClosestStandCell(Vector3Int agentCell, int startIdx, int count)
        {
            Vector3Int best = _allStandCells[startIdx];
            int bestDist = Math.Abs(agentCell.x - best.x) + Math.Abs(agentCell.y - best.y);

            for (int i = 1; i < count; i++)
            {
                Vector3Int c = _allStandCells[startIdx + i];
                int d = Math.Abs(agentCell.x - c.x) + Math.Abs(agentCell.y - c.y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }

            return best;
        }

        private Vector3Int PickClosestStandCellFromScratch(Vector3Int agentCell)
        {
            Vector3Int best = _scratchStand[0];
            int bestDist = Math.Abs(agentCell.x - best.x) + Math.Abs(agentCell.y - best.y);

            for (int i = 1; i < _scratchStand.Count; i++)
            {
                Vector3Int c = _scratchStand[i];
                int d = Math.Abs(agentCell.x - c.x) + Math.Abs(agentCell.y - c.y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }

            return best;
        }

        private static bool DoesCellBlockMovement(in PendingCell cellData, bool treatUnrecordedTilesAsBlocking)
        {
            if (cellData.Tile == null)
                return false;

            if (cellData.BuildableData != null)
                return cellData.BuildableData.blocksMovement;

            return treatUnrecordedTilesAsBlocking;
        }

        private static bool AreAllCellsWorkComplete(PendingOrder order)
        {
            var cells = order.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].IsWorkComplete)
                    return false;
            }
            return true;
        }
    }
}
