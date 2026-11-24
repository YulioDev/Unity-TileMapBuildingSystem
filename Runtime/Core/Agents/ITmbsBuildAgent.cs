using System.Collections.Generic;
using UnityEngine;

namespace TMBS.Core.Agents
{
    public interface ITmbsBuildAgent
    {
        GameObject AgentGameObject { get; }
        Vector3 WorldPosition { get; }
        TmbsAgentState State { get; }
        bool HasActiveJob { get; }
        TmbsAgentJob CurrentJob { get; }
        IReadOnlyList<Vector3Int> CurrentPath { get; }
        bool IsAvailableForJob { get; }
        void ReleaseCurrentJob();
    }
}
