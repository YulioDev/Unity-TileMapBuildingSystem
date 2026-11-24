using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Core.Agents
{
    /// <summary>
    /// Optional locomotion bridge allowing programmers to connect NavMesh, A* Pathfinding Project,
    /// Rigidbody2D, or custom movement scripts to any GameObject running TmbsBuildAgent.
    /// </summary>
    public interface ITmbsAgentMotor
    {
        void SetDestination(Vector3 targetWorldPosition, IReadOnlyList<Vector3Int> cellPath, Tilemap tilemap);
        void StopMovement();
        bool HasArrived(Vector3 targetWorldPosition, float arrivalTolerance);
    }
}
