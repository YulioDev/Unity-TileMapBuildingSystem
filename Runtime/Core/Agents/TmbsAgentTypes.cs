using UnityEngine;

namespace TMBS.Core.Agents
{
    public enum TmbsAgentState
    {
        Idle = 0,
        MovingToTarget = 1,
        SteppingAside = 2,
        DeliveringResources = 3,
        Building = 4,
        WaitingUnreachable = 5
    }

    public enum TmbsAgentTargetStrategy
    {
        SmartReachable = 0,
        Nearest = 1,
        OrderQueue = 2
    }

    public readonly struct TmbsAgentJob
    {
        public readonly int OrderId;
        public readonly Vector3Int TargetCell;
        public readonly Vector3Int StandCell;
        public readonly bool IsDemolition;
        public readonly bool BlocksMovement;
        public readonly bool IsValid;

        public TmbsAgentJob(
            int orderId,
            Vector3Int targetCell,
            Vector3Int standCell,
            bool isDemolition,
            bool blocksMovement)
        {
            OrderId = orderId;
            TargetCell = targetCell;
            StandCell = standCell;
            IsDemolition = isDemolition;
            BlocksMovement = blocksMovement;
            IsValid = true;
        }
    }
}
