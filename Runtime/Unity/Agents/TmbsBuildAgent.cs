using System;
using System.Collections.Generic;
using TMBS.Core.Agents;
using TMBS.Core.Pending;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Agents
{
    /// <summary>
    /// General GameObject builder agent component. Attach to any GameObject (unit, drone, NPC, player)
    /// to let it claim and construct pending TMBS orders using optional A* pathfinding or a custom motor.
    /// </summary>
    [AddComponentMenu("TMBS/Build Agent")]
    public sealed class TmbsBuildAgent : MonoBehaviour, ITmbsBuildAgent
    {
        [Header("Scene References")]
        [Tooltip("Target BuildeableTilemap. Auto-discovered in scene if omitted.")]
        [SerializeField] private BuildeableTilemap buildeableTilemap;

        [Tooltip("Optional central coordinator for reservations & shared pathfinding. Auto-discovered if omitted.")]
        [SerializeField] private TmbsBuildAgentManager agentManager;

        [Header("Job Selection & Pathfinding")]
        [Tooltip("When enabled, automatically searches and claims available pending cells while idle.")]
        [SerializeField] private bool autoFindJobs = true;

        [Tooltip("Enable TMBS's optional built-in A* grid pathfinder. Disable if a custom ITmbsAgentMotor handles full routing.")]
        [SerializeField] private bool useBuiltInPathfinding = true;

        [Tooltip("Allow 8-directional grid movement (still prevents diagonal corner-cutting around blocking tiles).")]
        [SerializeField] private bool allowDiagonalMovement = false;

        [Tooltip("Job prioritization heuristic. SmartReachable avoids trapping the agent or sealing interior pending cells.")]
        [SerializeField] private TmbsAgentTargetStrategy targetStrategy = TmbsAgentTargetStrategy.SmartReachable;

        [Tooltip("If the agent stands directly on a blocking cell it is about to build, automatically step onto an adjacent tile first.")]
        [SerializeField] private bool stepAsideWhenBlockingSelf = true;

        [Header("Movement & Work")]
        [Min(0.1f)]
        [Tooltip("Movement speed in world units/sec when using built-in transform movement.")]
        [SerializeField] private float moveSpeed = 4.5f;

        [Min(0.5f)]
        [Tooltip("Maximum cell radius within which the agent can stand to work on a target cell (1.5 covers cardinal & diagonal adjacent tiles).")]
        [SerializeField] private float buildRangeCells = 1.5f;

        [Min(0.01f)]
        [Tooltip("World distance threshold considered arrived at a waypoint or stand cell.")]
        [SerializeField] private float arrivalTolerance = 0.08f;

        [Min(1)]
        [Tooltip("Work units applied per work interval.")]
        [SerializeField] private int workPerTick = 1;

        [Min(0.02f)]
        [Tooltip("Seconds between work/resource steps once in range.")]
        [SerializeField] private float workInterval = 0.25f;

        [Tooltip("If true, the agent delivers required resources to the target cell automatically when in range.")]
        [SerializeField] private bool autoDeliverResources = true;

        [Min(1)]
        [Tooltip("Resources delivered per interval when Auto Deliver Resources is enabled.")]
        [SerializeField] private int resourcesPerTick = 1;

        [Min(0.05f)]
        [Tooltip("Seconds between pending job searches while idle.")]
        [SerializeField] private float jobSearchInterval = 0.25f;

        [Min(0.1f)]
        [Tooltip("Seconds between path re-evaluations while moving.")]
        [SerializeField] private float repathInterval = 0.5f;

        [Tooltip("Preserve the agent GameObject's current Z coordinate when moving in 2D tilemaps.")]
        [SerializeField] private bool preserveZAxis = true;

        [Header("Visual Feedback (Optional)")]
        [Tooltip("Tint the SpriteRenderer based on current agent state (ideal for procedural debug agents).")]
        [SerializeField] private bool syncTintWithState = false;

        [SerializeField] private SpriteRenderer visualRenderer;

        [Header("Unity Events")]
        [SerializeField] private UnityEvent<Vector3Int> onJobAssigned = new UnityEvent<Vector3Int>();
        [SerializeField] private UnityEvent<Vector3Int> onWorkApplied = new UnityEvent<Vector3Int>();
        [SerializeField] private UnityEvent<Vector3Int> onCellCompleted = new UnityEvent<Vector3Int>();
        [SerializeField] private UnityEvent<TmbsAgentState> onStateChanged = new UnityEvent<TmbsAgentState>();

        public event Action<TmbsBuildAgent, TmbsAgentJob> JobAssigned;
        public event Action<TmbsBuildAgent, TmbsAgentJob> ArrivedAtTarget;
        public event Action<TmbsBuildAgent, Vector3Int, int> ResourcesDelivered;
        public event Action<TmbsBuildAgent, Vector3Int, int> WorkApplied;
        public event Action<TmbsBuildAgent, Vector3Int> CellCompleted;
        public event Action<TmbsBuildAgent, TmbsAgentJob> JobReleased;
        public event Action<TmbsBuildAgent, TmbsAgentState> StateChanged;
        public event Action<TmbsBuildAgent, IReadOnlyList<Vector3Int>> PathComputed;

        private readonly List<Vector3Int> _currentPath = new List<Vector3Int>(64);
        private int _currentPathIndex;
        private TmbsAgentJob _currentJob;
        private bool _hasActiveJob;
        private TmbsAgentState _state = TmbsAgentState.Idle;
        private float _searchTimer;
        private float _workTimer;
        private float _repathTimer;
        private bool _arrivedNotifiedForJob;
        private bool _motorDestinationSentForJob;

        private ITmbsAgentMotor _customMotor;
        private TmbsTilemapWalkability _standaloneWalkability;
        private TmbsGridPathfinder _standalonePathfinder;
        private TmbsSmartJobPlanner _standalonePlanner;

        public GameObject AgentGameObject => gameObject;
        public Vector3 WorldPosition => transform.position;
        public TmbsAgentState State => _state;
        public bool HasActiveJob => _hasActiveJob;
        public TmbsAgentJob CurrentJob => _currentJob;
        public IReadOnlyList<Vector3Int> CurrentPath => _currentPath;
        public int CurrentPathIndex => _currentPathIndex;
        public bool IsAvailableForJob => isActiveAndEnabled && !_hasActiveJob;
        public BuildeableTilemap TargetBuildeableTilemap => buildeableTilemap;
        public TmbsBuildAgentManager Manager => agentManager;

        public bool AutoFindJobs
        {
            get => autoFindJobs;
            set => autoFindJobs = value;
        }

        public bool UseBuiltInPathfinding
        {
            get => useBuiltInPathfinding;
            set => useBuiltInPathfinding = value;
        }

        public bool AllowDiagonalMovement
        {
            get => allowDiagonalMovement;
            set => allowDiagonalMovement = value;
        }

        public TmbsAgentTargetStrategy TargetStrategy
        {
            get => targetStrategy;
            set => targetStrategy = value;
        }

        public bool StepAsideWhenBlockingSelf
        {
            get => stepAsideWhenBlockingSelf;
            set => stepAsideWhenBlockingSelf = value;
        }

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0.1f, value);
        }

        public float BuildRangeCells
        {
            get => buildRangeCells;
            set => buildRangeCells = Mathf.Max(0.5f, value);
        }

        public float ArrivalTolerance
        {
            get => arrivalTolerance;
            set => arrivalTolerance = Mathf.Max(0.01f, value);
        }

        public int WorkPerTick
        {
            get => workPerTick;
            set => workPerTick = Mathf.Max(1, value);
        }

        public float WorkInterval
        {
            get => workInterval;
            set => workInterval = Mathf.Max(0.02f, value);
        }

        public bool AutoDeliverResources
        {
            get => autoDeliverResources;
            set => autoDeliverResources = value;
        }

        public int ResourcesPerTick
        {
            get => resourcesPerTick;
            set => resourcesPerTick = Mathf.Max(1, value);
        }

        public bool SyncTintWithState
        {
            get => syncTintWithState;
            set
            {
                syncTintWithState = value;
                UpdateVisualTint();
            }
        }

        public static TmbsBuildAgent AttachTo(
            GameObject target,
            BuildeableTilemap tilemap = null,
            TmbsBuildAgentManager manager = null)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var agent = target.GetComponent<TmbsBuildAgent>();
            if (agent == null)
                agent = target.AddComponent<TmbsBuildAgent>();

            if (manager != null)
            {
                if (tilemap != null && manager.BuildeableTilemap == null)
                    manager.BuildeableTilemap = tilemap;
                manager.RegisterAgent(target);
            }
            else if (tilemap != null)
            {
                agent.Initialize(tilemap, null);
            }

            return agent;
        }

        public void Initialize(BuildeableTilemap tilemap, TmbsBuildAgentManager manager = null)
        {
            if (tilemap != null)
                buildeableTilemap = tilemap;
            if (manager != null)
                agentManager = manager;

            ResolveMotorAndVisual();
            UpdateVisualTint();
        }

        public void SetCustomMotor(ITmbsAgentMotor motor)
        {
            _customMotor = motor;
            _motorDestinationSentForJob = false;
        }

        public bool TryAssignCell(Vector3Int targetCell)
        {
            EnsureReferences();
            if (buildeableTilemap == null || buildeableTilemap.PendingWorkApi == null || buildeableTilemap.TargetTilemap == null)
                return false;

            var tilemap = buildeableTilemap.TargetTilemap;
            var workApi = buildeableTilemap.PendingWorkApi;
            Vector3Int agentCell = tilemap.WorldToCell(transform.position);

            ReleaseCurrentJob();

            TmbsAgentJob job;
            bool planned;

            if (agentManager != null)
            {
                planned = agentManager.TryPlanSpecificCell(this, agentCell, targetCell, _currentPath, out job);
            }
            else
            {
                EnsureStandaloneTools();
                planned = TryPlanStandaloneSpecificCell(agentCell, targetCell, workApi, out job);
            }

            if (!planned)
                return false;

            AcceptAssignedJob(job);
            return true;
        }

        public void ReleaseCurrentJob()
        {
            if (!_hasActiveJob)
                return;

            var released = _currentJob;
            _hasActiveJob = false;
            _currentJob = default;
            _currentPath.Clear();
            _currentPathIndex = 0;
            _workTimer = 0f;
            _repathTimer = 0f;
            _arrivedNotifiedForJob = false;
            _motorDestinationSentForJob = false;

            _customMotor?.StopMovement();

            if (agentManager != null)
                agentManager.ReleaseReservationsFor(this);

            SetState(TmbsAgentState.Idle);
            JobReleased?.Invoke(this, released);
        }

        private void Awake()
        {
            ResolveMotorAndVisual();
        }

        private void OnEnable()
        {
            EnsureReferences();
            ResolveMotorAndVisual();
            if (agentManager != null)
                agentManager.RegisterAgent(this);
            UpdateVisualTint();
        }

        private void OnDisable()
        {
            ReleaseCurrentJob();
            if (agentManager != null)
                agentManager.UnregisterAgent(this);
        }

        private void OnDestroy()
        {
            if (_hasActiveJob && agentManager != null)
                agentManager.ReleaseReservationsFor(this);
            if (agentManager != null)
                agentManager.UnregisterAgent(this);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
                return;

            TickAgent(Time.deltaTime);
        }

        public void TickAgent(float deltaTime)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            EnsureReferences();
            if (buildeableTilemap == null || buildeableTilemap.TargetTilemap == null || buildeableTilemap.PendingWorkApi == null)
            {
                if (_hasActiveJob)
                    ReleaseCurrentJob();
                SetState(TmbsAgentState.Idle);
                return;
            }

            var tilemap = buildeableTilemap.TargetTilemap;
            var workApi = buildeableTilemap.PendingWorkApi;

            if (!_hasActiveJob)
            {
                TickIdleSearch(deltaTime, tilemap, workApi);
                return;
            }

            if (!workApi.TryGetCellStatus(_currentJob.TargetCell, out var cellStatus) || cellStatus.IsCompleted)
            {
                ReleaseCurrentJob();
                return;
            }

            Vector3Int agentCell = tilemap.WorldToCell(transform.position);
            ITmbsGridWalkabilityProvider walkability = ResolveWalkabilityProvider();

            if (_currentJob.BlocksMovement && stepAsideWhenBlockingSelf && agentCell == _currentJob.TargetCell)
            {
                if (_currentJob.StandCell == _currentJob.TargetCell || !IsCellWalkable(walkability, _currentJob.StandCell))
                {
                    if (!TmbsGridPathfinder.TryFindStepAsideCell(agentCell, _currentJob.TargetCell, walkability, out var stepAsideCell))
                    {
                        ReleaseCurrentJob();
                        return;
                    }

                    _currentJob = new TmbsAgentJob(
                        _currentJob.OrderId,
                        _currentJob.TargetCell,
                        stepAsideCell,
                        _currentJob.IsDemolition,
                        _currentJob.BlocksMovement);
                    _currentPath.Clear();
                    _currentPath.Add(agentCell);
                    _currentPath.Add(stepAsideCell);
                    _currentPathIndex = 1;
                    _motorDestinationSentForJob = false;
                }
            }

            Vector3 standWorldPos = GetCellWorldPos(tilemap, _currentJob.StandCell);
            bool onForbiddenTarget = _currentJob.BlocksMovement && agentCell == _currentJob.TargetCell;
            bool arrived = !onForbiddenTarget && HasArrivedAtStandPosition(standWorldPos);

            if (!arrived)
            {
                _workTimer = 0f;
                SetState(onForbiddenTarget ? TmbsAgentState.SteppingAside : TmbsAgentState.MovingToTarget);
                TickMovement(deltaTime, tilemap, agentCell, standWorldPos, walkability);
                return;
            }

            if (!_arrivedNotifiedForJob)
            {
                _arrivedNotifiedForJob = true;
                _customMotor?.StopMovement();
                ArrivedAtTarget?.Invoke(this, _currentJob);
            }

            TickWork(deltaTime, workApi, in cellStatus);
        }

        private void TickIdleSearch(float deltaTime, Tilemap tilemap, IPendingWorkApi workApi)
        {
            if (!autoFindJobs)
            {
                SetState(TmbsAgentState.Idle);
                return;
            }

            var orders = workApi.GetAll();
            if (!HasIncompleteCells(orders))
            {
                _searchTimer = jobSearchInterval;
                SetState(TmbsAgentState.Idle);
                return;
            }

            _searchTimer += deltaTime;
            if (_searchTimer < jobSearchInterval && _state == TmbsAgentState.WaitingUnreachable)
                return;

            _searchTimer = 0f;
            Vector3Int agentCell = tilemap.WorldToCell(transform.position);

            TmbsAgentJob job;
            bool claimed;
            if (agentManager != null)
            {
                claimed = agentManager.TryClaimBestJob(this, agentCell, _currentPath, out job);
            }
            else
            {
                EnsureStandaloneTools();
                claimed = TryClaimStandaloneBestJob(agentCell, workApi, out job);
            }

            if (claimed)
            {
                AcceptAssignedJob(job);
            }
            else
            {
                SetState(TmbsAgentState.WaitingUnreachable);
            }
        }

        private void TickMovement(
            float deltaTime,
            Tilemap tilemap,
            Vector3Int agentCell,
            Vector3 standWorldPos,
            ITmbsGridWalkabilityProvider walkability)
        {
            if (useBuiltInPathfinding)
            {
                _repathTimer += deltaTime;
                bool needsRepath = _currentPath.Count == 0 ||
                                   _repathTimer >= repathInterval ||
                                   IsRemainingPathBlocked(walkability);

                if (needsRepath && agentCell != _currentJob.StandCell)
                {
                    _repathTimer = 0f;
                    bool repathed = TryRecomputePath(agentCell, _currentJob.StandCell, walkability);
                    if (!repathed)
                    {
                        ReleaseCurrentJob();
                        SetState(TmbsAgentState.WaitingUnreachable);
                        return;
                    }
                }
            }

            if (_customMotor != null)
            {
                if (!_motorDestinationSentForJob)
                {
                    _motorDestinationSentForJob = true;
                    _customMotor.SetDestination(standWorldPos, _currentPath, tilemap);
                }
                return;
            }

            MoveDirectlyOrAlongPath(deltaTime, tilemap, standWorldPos);
        }

        private void MoveDirectlyOrAlongPath(float deltaTime, Tilemap tilemap, Vector3 standWorldPos)
        {
            float maxStep = Mathf.Max(0.1f, moveSpeed) * deltaTime;
            if (maxStep <= 0f)
                return;

            if (!useBuiltInPathfinding || _currentPath.Count == 0)
            {
                MoveTransformTowards(standWorldPos, maxStep);
                return;
            }

            if (_currentPathIndex < 0)
                _currentPathIndex = 0;

            while (_currentPathIndex < _currentPath.Count)
            {
                Vector3 wp = GetCellWorldPos(tilemap, _currentPath[_currentPathIndex]);
                if (GetDistance(transform.position, wp) <= Mathf.Max(arrivalTolerance * 0.5f, 0.02f))
                {
                    if (_currentPathIndex == _currentPath.Count - 1)
                        break;
                    _currentPathIndex++;
                    continue;
                }

                MoveTransformTowards(wp, maxStep);
                break;
            }
        }

        private void MoveTransformTowards(Vector3 target, float maxStep)
        {
            Vector3 current = transform.position;
            if (preserveZAxis)
            {
                target.z = current.z;
                Vector2 next2D = Vector2.MoveTowards(new Vector2(current.x, current.y), new Vector2(target.x, target.y), maxStep);
                transform.position = new Vector3(next2D.x, next2D.y, current.z);
            }
            else
            {
                transform.position = Vector3.MoveTowards(current, target, maxStep);
            }
        }

        private void TickWork(float deltaTime, IPendingWorkApi workApi, in PendingCell cellStatus)
        {
            ResourceRequirement policy = GetResourcePolicy();

            if (cellStatus.ResourceDelivered < cellStatus.ResourceRequired && !autoDeliverResources)
            {
                if (cellStatus.WorkAllowed(policy) <= cellStatus.WorkDone && !cellStatus.IsWorkComplete)
                {
                    ReleaseCurrentJob();
                    return;
                }
            }

            float interval = Mathf.Max(0.02f, workInterval);
            _workTimer += deltaTime;
            if (_workTimer < interval)
            {
                SetState(cellStatus.ResourceDelivered < cellStatus.ResourceRequired && cellStatus.WorkAllowed(policy) <= cellStatus.WorkDone
                    ? TmbsAgentState.DeliveringResources
                    : TmbsAgentState.Building);
                return;
            }

            _workTimer -= interval;
            if (_workTimer > interval)
                _workTimer = 0f;

            PendingCell workingCell = cellStatus;

            if (workingCell.ResourceDelivered < workingCell.ResourceRequired && autoDeliverResources)
            {
                SetState(TmbsAgentState.DeliveringResources);
                int needed = workingCell.ResourceRequired - workingCell.ResourceDelivered;
                int deliverAmount = Mathf.Min(Mathf.Max(1, resourcesPerTick), needed);
                if (deliverAmount > 0 && workApi.TryDeliverResources(_currentJob.TargetCell, deliverAmount))
                {
                    ResourcesDelivered?.Invoke(this, _currentJob.TargetCell, deliverAmount);
                }

                if (!workApi.TryGetCellStatus(_currentJob.TargetCell, out workingCell) || workingCell.IsCompleted)
                {
                    NotifyCompletedAndRelease(_currentJob.TargetCell);
                    return;
                }

                if (workingCell.WorkAllowed(policy) <= workingCell.WorkDone && !workingCell.IsWorkComplete)
                    return;
            }

            SetState(TmbsAgentState.Building);

            if (!workingCell.IsWorkComplete)
            {
                int allowed = workingCell.WorkAllowed(policy);
                int remaining = allowed - workingCell.WorkDone;
                if (remaining > 0)
                {
                    int add = Mathf.Min(Mathf.Max(1, workPerTick), remaining);
                    if (workApi.TryAddWork(_currentJob.TargetCell, add))
                    {
                        WorkApplied?.Invoke(this, _currentJob.TargetCell, add);
                        onWorkApplied?.Invoke(_currentJob.TargetCell);
                    }
                }
            }

            if (!workApi.TryGetCellStatus(_currentJob.TargetCell, out var afterWork) || afterWork.IsCompleted)
            {
                NotifyCompletedAndRelease(_currentJob.TargetCell);
                return;
            }

            if (afterWork.IsWorkComplete)
            {
                Vector3Int completedTarget = _currentJob.TargetCell;
                if (workApi.TryCompleteCell(completedTarget))
                {
                    NotifyCompletedAndRelease(completedTarget);
                }
                else
                {
                    // Multi-cell order with allowPartialCompletion == false: release finished cell so
                    // this agent or another agent can complete the remaining sibling cells.
                    ReleaseCurrentJob();
                }
            }
        }

        private void NotifyCompletedAndRelease(Vector3Int cell)
        {
            CellCompleted?.Invoke(this, cell);
            onCellCompleted?.Invoke(cell);
            ReleaseCurrentJob();
        }

        private void AcceptAssignedJob(TmbsAgentJob job)
        {
            _currentJob = job;
            _hasActiveJob = true;
            _currentPathIndex = _currentPath.Count > 1 ? 1 : 0;
            _workTimer = 0f;
            _repathTimer = 0f;
            _arrivedNotifiedForJob = false;
            _motorDestinationSentForJob = false;

            SetState(TmbsAgentState.MovingToTarget);
            PathComputed?.Invoke(this, _currentPath);
            JobAssigned?.Invoke(this, _currentJob);
            onJobAssigned?.Invoke(_currentJob.TargetCell);
        }

        private bool TryRecomputePath(
            Vector3Int agentCell,
            Vector3Int standCell,
            ITmbsGridWalkabilityProvider walkability)
        {
            bool ok;
            if (agentManager != null)
            {
                ok = agentManager.TryComputePath(agentCell, standCell, _currentPath, allowDiagonalMovement);
            }
            else
            {
                EnsureStandaloneTools();
                ok = _standalonePathfinder.TryFindPath(agentCell, standCell, walkability, _currentPath, allowDiagonalMovement);
            }

            if (ok)
            {
                _currentPathIndex = _currentPath.Count > 1 ? 1 : 0;
                _motorDestinationSentForJob = false;
                PathComputed?.Invoke(this, _currentPath);
            }

            return ok;
        }

        private bool IsRemainingPathBlocked(ITmbsGridWalkabilityProvider walkability)
        {
            if (walkability == null || _currentPath.Count == 0)
                return false;

            int start = Mathf.Max(1, _currentPathIndex);
            for (int i = start; i < _currentPath.Count; i++)
            {
                if (!walkability.IsWalkable(_currentPath[i]))
                    return true;
            }
            return false;
        }

        private bool HasArrivedAtStandPosition(Vector3 standWorldPos)
        {
            if (_customMotor != null)
                return _customMotor.HasArrived(standWorldPos, arrivalTolerance);

            return GetDistance(transform.position, standWorldPos) <= arrivalTolerance;
        }

        private float GetDistance(Vector3 a, Vector3 b)
        {
            if (preserveZAxis)
                return Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
            return Vector3.Distance(a, b);
        }

        private Vector3 GetCellWorldPos(Tilemap tilemap, Vector3Int cell)
        {
            Vector3 world = tilemap.GetCellCenterWorld(cell);
            if (preserveZAxis)
                world.z = transform.position.z;
            return world;
        }

        private ITmbsGridWalkabilityProvider ResolveWalkabilityProvider()
        {
            if (agentManager != null)
                return agentManager.WalkabilityProvider;

            EnsureStandaloneTools();
            return _standaloneWalkability;
        }

        private bool TryClaimStandaloneBestJob(Vector3Int agentCell, IPendingWorkApi workApi, out TmbsAgentJob job)
        {
            var cfg = buildeableTilemap.RootConfig != null ? buildeableTilemap.RootConfig.GetPendingConfig() : null;
            ResourceRequirement policy = cfg != null ? cfg.resourcePolicy : ResourceRequirement.None;
            bool allowPartial = cfg == null || cfg.allowPartialCompletion;

            return _standalonePlanner.TryPlanBestJob(
                agentCell,
                workApi.GetAll(),
                policy,
                autoDeliverResources,
                allowPartial,
                treatUnrecordedTilesAsBlocking: false,
                buildRangeCells,
                useBuiltInPathfinding,
                allowDiagonalMovement,
                targetStrategy,
                _standaloneWalkability,
                _standalonePathfinder,
                isCellReserved: null,
                _currentPath,
                out job);
        }

        private bool TryPlanStandaloneSpecificCell(
            Vector3Int agentCell,
            Vector3Int targetCell,
            IPendingWorkApi workApi,
            out TmbsAgentJob job)
        {
            job = default;
            if (!workApi.TryGetCellStatus(targetCell, out var cellStatus))
                return false;

            int orderId = FindOrderIdForCell(workApi.GetAll(), targetCell);
            return _standalonePlanner.TryPlanSpecificCell(
                agentCell,
                targetCell,
                orderId,
                in cellStatus,
                treatUnrecordedTilesAsBlocking: false,
                buildRangeCells,
                useBuiltInPathfinding,
                allowDiagonalMovement,
                _standaloneWalkability,
                _standalonePathfinder,
                _currentPath,
                out job);
        }

        private void EnsureStandaloneTools()
        {
            if (_standaloneWalkability == null)
                _standaloneWalkability = new TmbsTilemapWalkability(buildeableTilemap.Metadata, buildeableTilemap.TargetTilemap);
            else
                _standaloneWalkability.SetSources(buildeableTilemap.Metadata, buildeableTilemap.TargetTilemap);

            if (_standalonePathfinder == null)
                _standalonePathfinder = new TmbsGridPathfinder();

            if (_standalonePlanner == null)
                _standalonePlanner = new TmbsSmartJobPlanner();
        }

        private void EnsureReferences()
        {
            if (buildeableTilemap == null)
            {
#if UNITY_2023_1_OR_NEWER
                buildeableTilemap = FindAnyObjectByType<BuildeableTilemap>();
#else
                buildeableTilemap = FindObjectOfType<BuildeableTilemap>();
#endif
            }

            if (agentManager == null)
            {
#if UNITY_2023_1_OR_NEWER
                agentManager = FindAnyObjectByType<TmbsBuildAgentManager>();
#else
                agentManager = FindObjectOfType<TmbsBuildAgentManager>();
#endif
            }
        }

        private void ResolveMotorAndVisual()
        {
            if (_customMotor == null)
                _customMotor = GetComponent<ITmbsAgentMotor>();

            if (visualRenderer == null)
                visualRenderer = GetComponent<SpriteRenderer>();
        }

        private ResourceRequirement GetResourcePolicy()
        {
            if (buildeableTilemap != null && buildeableTilemap.RootConfig != null)
            {
                var cfg = buildeableTilemap.RootConfig.GetPendingConfig();
                if (cfg != null)
                    return cfg.resourcePolicy;
            }
            return ResourceRequirement.None;
        }

        private void SetState(TmbsAgentState next)
        {
            if (_state == next)
                return;

            _state = next;
            UpdateVisualTint();
            StateChanged?.Invoke(this, _state);
            onStateChanged?.Invoke(_state);
        }

        private void UpdateVisualTint()
        {
            if (syncTintWithState && visualRenderer != null)
                visualRenderer.color = TmbsAgentVisualFactory.GetStateColor(_state);
        }

        private static bool IsCellWalkable(ITmbsGridWalkabilityProvider walkability, Vector3Int cell)
        {
            return walkability == null || walkability.IsWalkable(cell);
        }

        private static bool HasIncompleteCells(IReadOnlyList<PendingOrder> orders)
        {
            if (orders == null || orders.Count == 0)
                return false;

            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null || order.Cells == null)
                    continue;
                for (int c = 0; c < order.Cells.Length; c++)
                {
                    if (!order.Cells[c].IsCompleted)
                        return true;
                }
            }

            return false;
        }

        private static int FindOrderIdForCell(IReadOnlyList<PendingOrder> orders, Vector3Int cell)
        {
            if (orders == null)
                return 0;

            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null || order.Cells == null)
                    continue;
                for (int c = 0; c < order.Cells.Length; c++)
                {
                    if (order.Cells[c].Cell == cell)
                        return order.Id;
                }
            }

            return 0;
        }
    }
}
