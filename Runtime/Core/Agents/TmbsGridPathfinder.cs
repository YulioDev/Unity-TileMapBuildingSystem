using System;
using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Agents
{
    public sealed class TmbsGridPathfinder : ITmbsGridPathfinder
    {
        private readonly struct HeapNode
        {
            public readonly Vector3Int Cell;
            public readonly int F;
            public readonly int G;

            public HeapNode(Vector3Int cell, int f, int g)
            {
                Cell = cell;
                F = f;
                G = g;
            }
        }

        private const int CardinalCost = 10;
        private const int DiagonalCost = 14;

        private static readonly Vector3Int[] CardinalDirs =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0)
        };

        private static readonly Vector3Int[] AllDirs =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, -1, 0),
            new Vector3Int(-1, 1, 0),
            new Vector3Int(-1, -1, 0)
        };

        private readonly List<HeapNode> _heap = new List<HeapNode>(128);
        private readonly Dictionary<Vector3Int, int> _gScore = new Dictionary<Vector3Int, int>(128);
        private readonly Dictionary<Vector3Int, Vector3Int> _cameFrom = new Dictionary<Vector3Int, Vector3Int>(128);
        private readonly HashSet<Vector3Int> _closed = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> _goalSet = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> _walkableGoals = new List<Vector3Int>(16);
        private readonly Vector3Int[] _singleGoalBuffer = new Vector3Int[1];

        public bool TryFindPath(
            Vector3Int start,
            Vector3Int goal,
            ITmbsGridWalkabilityProvider walkability,
            List<Vector3Int> pathBuffer,
            bool allowDiagonal = false,
            int maxExpandedNodes = 2048)
        {
            _singleGoalBuffer[0] = goal;
            return TryFindPathToAnyGoal(
                start,
                _singleGoalBuffer,
                walkability,
                pathBuffer,
                out _,
                allowDiagonal,
                maxExpandedNodes);
        }

        public bool TryFindPathToAnyGoal(
            Vector3Int start,
            IReadOnlyList<Vector3Int> goals,
            ITmbsGridWalkabilityProvider walkability,
            List<Vector3Int> pathBuffer,
            out Vector3Int reachedGoal,
            bool allowDiagonal = false,
            int maxExpandedNodes = 2048)
        {
            if (pathBuffer == null)
                throw new ArgumentNullException(nameof(pathBuffer));

            pathBuffer.Clear();
            reachedGoal = default;

            if (goals == null || goals.Count == 0)
                return false;

            _goalSet.Clear();
            _walkableGoals.Clear();

            for (int i = 0; i < goals.Count; i++)
            {
                Vector3Int g = new Vector3Int(goals[i].x, goals[i].y, start.z);
                if (walkability != null && !walkability.IsWalkable(g))
                    continue;

                if (_goalSet.Add(g))
                    _walkableGoals.Add(g);
            }

            if (_walkableGoals.Count == 0)
                return false;

            if (_goalSet.Contains(start))
            {
                pathBuffer.Add(start);
                reachedGoal = start;
                return true;
            }

            _heap.Clear();
            _gScore.Clear();
            _cameFrom.Clear();
            _closed.Clear();

            int limit = Mathf.Max(1, maxExpandedNodes);
            int expanded = 0;
            Vector3Int[] dirs = allowDiagonal ? AllDirs : CardinalDirs;

            _gScore[start] = 0;
            PushHeap(new HeapNode(start, EstimateMinHeuristic(start, allowDiagonal), 0));

            while (_heap.Count > 0)
            {
                var current = PopHeap();
                if (_closed.Contains(current.Cell))
                    continue;

                _closed.Add(current.Cell);

                if (_goalSet.Contains(current.Cell))
                {
                    ReconstructPath(start, current.Cell, pathBuffer);
                    reachedGoal = current.Cell;
                    return true;
                }

                expanded++;
                if (expanded >= limit)
                    break;

                for (int i = 0; i < dirs.Length; i++)
                {
                    Vector3Int dir = dirs[i];
                    Vector3Int next = new Vector3Int(current.Cell.x + dir.x, current.Cell.y + dir.y, start.z);
                    if (_closed.Contains(next))
                        continue;

                    if (walkability != null && !walkability.IsWalkable(next))
                        continue;

                    bool isDiagonal = dir.x != 0 && dir.y != 0;
                    if (isDiagonal && walkability != null)
                    {
                        Vector3Int adjX = new Vector3Int(current.Cell.x + dir.x, current.Cell.y, start.z);
                        Vector3Int adjY = new Vector3Int(current.Cell.x, current.Cell.y + dir.y, start.z);
                        if (!walkability.IsWalkable(adjX) || !walkability.IsWalkable(adjY))
                            continue;
                    }

                    int tentativeG = current.G + (isDiagonal ? DiagonalCost : CardinalCost);
                    if (_gScore.TryGetValue(next, out int prevG) && tentativeG >= prevG)
                        continue;

                    _gScore[next] = tentativeG;
                    _cameFrom[next] = current.Cell;
                    int f = tentativeG + EstimateMinHeuristic(next, allowDiagonal);
                    PushHeap(new HeapNode(next, f, tentativeG));
                }
            }

            return false;
        }

        private int EstimateMinHeuristic(Vector3Int cell, bool allowDiagonal)
        {
            int best = int.MaxValue;
            for (int i = 0; i < _walkableGoals.Count; i++)
            {
                Vector3Int g = _walkableGoals[i];
                int dx = Math.Abs(cell.x - g.x);
                int dy = Math.Abs(cell.y - g.y);
                int h = allowDiagonal
                    ? CardinalCost * (dx + dy) + (DiagonalCost - 2 * CardinalCost) * Math.Min(dx, dy)
                    : CardinalCost * (dx + dy);
                if (h < best)
                    best = h;
            }
            return best == int.MaxValue ? 0 : best;
        }

        private void ReconstructPath(Vector3Int start, Vector3Int goal, List<Vector3Int> pathBuffer)
        {
            Vector3Int cur = goal;
            pathBuffer.Add(cur);
            while (cur != start && _cameFrom.TryGetValue(cur, out var prev))
            {
                cur = prev;
                pathBuffer.Add(cur);
            }
            pathBuffer.Reverse();
        }

        private void PushHeap(HeapNode node)
        {
            _heap.Add(node);
            int idx = _heap.Count - 1;
            while (idx > 0)
            {
                int parent = (idx - 1) >> 1;
                if (CompareHeap(_heap[idx], _heap[parent]) >= 0)
                    break;

                var tmp = _heap[idx];
                _heap[idx] = _heap[parent];
                _heap[parent] = tmp;
                idx = parent;
            }
        }

        private HeapNode PopHeap()
        {
            HeapNode root = _heap[0];
            int lastIdx = _heap.Count - 1;
            _heap[0] = _heap[lastIdx];
            _heap.RemoveAt(lastIdx);

            int count = _heap.Count;
            int idx = 0;
            while (true)
            {
                int left = (idx << 1) + 1;
                int right = left + 1;
                if (left >= count)
                    break;

                int smallest = (right < count && CompareHeap(_heap[right], _heap[left]) < 0)
                    ? right
                    : left;

                if (CompareHeap(_heap[smallest], _heap[idx]) >= 0)
                    break;

                var tmp = _heap[idx];
                _heap[idx] = _heap[smallest];
                _heap[smallest] = tmp;
                idx = smallest;
            }

            return root;
        }

        private static int CompareHeap(in HeapNode a, in HeapNode b)
        {
            int cmp = a.F.CompareTo(b.F);
            if (cmp != 0)
                return cmp;

            int hA = a.F - a.G;
            int hB = b.F - b.G;
            return hA.CompareTo(hB);
        }

        public static void CollectValidStandCells(
            Vector3Int targetCell,
            float buildRangeCells,
            bool allowStandingOnTarget,
            bool allowDiagonalReach,
            ITmbsGridWalkabilityProvider walkability,
            List<Vector3Int> results,
            Vector3Int? forbiddenCell = null)
        {
            results.Clear();

            bool IsAllowed(Vector3Int c)
            {
                if (forbiddenCell.HasValue && c == forbiddenCell.Value)
                    return false;
                return walkability == null || walkability.IsWalkable(c);
            }

            if (allowStandingOnTarget && IsAllowed(targetCell))
                results.Add(targetCell);

            float effectiveRange = Mathf.Max(1f, buildRangeCells);
            int radius = Mathf.Clamp(Mathf.FloorToInt(effectiveRange), 1, 6);
            float maxDistSq = effectiveRange * effectiveRange + 0.001f;

            // Cardinal neighbors first for clean alignment and tie breaking.
            for (int i = 0; i < CardinalDirs.Length; i++)
            {
                Vector3Int c = targetCell + CardinalDirs[i];
                if (IsAllowed(c))
                    results.Add(c);
            }

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int manhattan = Math.Abs(dx) + Math.Abs(dy);
                    if (manhattan == 1)
                        continue; // Already added above.

                    if (!allowDiagonalReach && radius == 1)
                        continue;

                    if (dx * dx + dy * dy > maxDistSq)
                        continue;

                    Vector3Int c = new Vector3Int(targetCell.x + dx, targetCell.y + dy, targetCell.z);
                    if (IsAllowed(c))
                        results.Add(c);
                }
            }
        }

        public static bool TryFindStepAsideCell(
            Vector3Int currentCell,
            Vector3Int cellToVacate,
            ITmbsGridWalkabilityProvider walkability,
            out Vector3Int stepAsideCell)
        {
            for (int i = 0; i < AllDirs.Length; i++)
            {
                Vector3Int candidate = currentCell + AllDirs[i];
                if (candidate == cellToVacate)
                    continue;

                if (walkability == null || walkability.IsWalkable(candidate))
                {
                    stepAsideCell = candidate;
                    return true;
                }
            }

            stepAsideCell = default;
            return false;
        }

        public static bool TryFindNearestWalkableCell(
            Vector3Int origin,
            ITmbsGridWalkabilityProvider walkability,
            out Vector3Int result,
            int maxRadius = 8)
        {
            if (walkability == null || walkability.IsWalkable(origin))
            {
                result = origin;
                return true;
            }

            int clampedRadius = Mathf.Clamp(maxRadius, 1, 32);
            for (int r = 1; r <= clampedRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                            continue;

                        Vector3Int candidate = new Vector3Int(origin.x + dx, origin.y + dy, origin.z);
                        if (walkability.IsWalkable(candidate))
                        {
                            result = candidate;
                            return true;
                        }
                    }
                }
            }

            result = origin;
            return false;
        }
    }
}
