using System;
using System.Collections.Generic;
using TMBS.Core.Agents;
using TMBS.Core.Pending;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Unity.Agents
{
    /// <summary>
    /// Coordinates builder agents, cell reservations, GameObject spawning, and optional smart A* pathfinding.
    /// Any GameObject can be spawned or registered as a builder agent via SpawnAgent or RegisterAgent.
    /// </summary>
    [AddComponentMenu("TMBS/Build Agent Manager")]
    public sealed class TmbsBuildAgentManager : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Target BuildeableTilemap instance. Auto-discovered if omitted.")]
        [SerializeField] private BuildeableTilemap buildeableTilemap;

        [Tooltip("Optional prefab (any GameObject) used when spawning agents. If null, a procedural 2D agent GameObject is created.")]
        [SerializeField] private GameObject defaultAgentPrefab;

        [Tooltip("Optional parent transform for newly spawned agent GameObjects.")]
        [SerializeField] private Transform agentsContainer;

        [Header("Spawning")]
        [Min(0)]
        [Tooltip("Optional number of agents to spawn automatically on Start.")]
        [SerializeField] private int spawnOnStartCount = 0;

        [SerializeField] private Vector3 initialSpawnOffset = Vector3.zero;

        [Min(0f)]
        [Tooltip("Radial spread distance when spawning multiple agents together.")]
        [SerializeField] private float spawnSpreadRadius = 1.5f;

        [Header("Optional Pathfinding & Walkability")]
        [Tooltip("Enable TMBS's optional built-in A* grid pathfinder by default for spawned agents.")]
        [SerializeField] private bool useBuiltInPathfinding = true;

        [Tooltip("Allow 8-directional grid paths by default.")]
        [SerializeField] private bool allowDiagonalMovement = false;

        [Tooltip("Default target selection heuristic. SmartReachable prevents agents from walling off interior cells.")]
        [SerializeField] private TmbsAgentTargetStrategy defaultTargetStrategy = TmbsAgentTargetStrategy.SmartReachable;

        [Min(64)]
        [Tooltip("Safety limit on expanded A* nodes per query.")]
        [SerializeField] private int maxPathExpandedNodes = 2048;

        [Tooltip("Treat existing target tilemap tiles without TmbsBuildableData metadata as non-walkable obstacles.")]
        [SerializeField] private bool treatUnrecordedTilesAsBlocking = false;

        [Tooltip("Optional additional obstacle tilemaps (rivers, cliffs, walls) considered non-walkable.")]
        [SerializeField] private Tilemap[] extraObstacleTilemaps;

        [Header("Default Agent Stats")]
        [Min(0.1f)]
        [SerializeField] private float defaultMoveSpeed = 4.5f;

        [Min(0.5f)]
        [SerializeField] private float defaultBuildRangeCells = 1.5f;

        [Min(1)]
        [SerializeField] private int defaultWorkPerTick = 1;

        [Min(0.02f)]
        [SerializeField] private float defaultWorkInterval = 0.25f;

        [SerializeField] private bool defaultAutoDeliverResources = true;

        [Min(1)]
        [SerializeField] private int defaultResourcesPerTick = 1;

        public event Action<GameObject> AgentSpawned;
        public event Action<GameObject> AgentDespawned;

        private readonly List<TmbsBuildAgent> _agents = new List<TmbsBuildAgent>();
        private readonly List<GameObject> _agentObjects = new List<GameObject>();
        private readonly Dictionary<Vector3Int, TmbsBuildAgent> _reservedCells = new Dictionary<Vector3Int, TmbsBuildAgent>();
        private readonly List<Vector3Int> _staleReservationKeys = new List<Vector3Int>(16);

        private TmbsTilemapWalkability _defaultWalkability;
        private ITmbsGridWalkabilityProvider _customWalkabilityProvider;
        private ITmbsGridPathfinder _pathfinder;
        private TmbsSmartJobPlanner _planner;
        private Func<Vector3Int, bool> _customWalkablePredicate;
        private BoundsInt? _walkableBounds;
        private int _spawnSequence;

        public BuildeableTilemap BuildeableTilemap
        {
            get => buildeableTilemap;
            set
            {
                buildeableTilemap = value;
                SyncWalkabilitySources();
            }
        }

        public GameObject DefaultAgentPrefab
        {
            get => defaultAgentPrefab;
            set => defaultAgentPrefab = value;
        }

        public Transform AgentsContainer
        {
            get => agentsContainer;
            set => agentsContainer = value;
        }

        public IReadOnlyList<GameObject> ActiveAgentObjects
        {
            get
            {
                PruneDestroyedAgents();
                return _agentObjects;
            }
        }

        public IReadOnlyList<TmbsBuildAgent> ActiveAgents
        {
            get
            {
                PruneDestroyedAgents();
                return _agents;
            }
        }

        public int AgentCount
        {
            get
            {
                PruneDestroyedAgents();
                return _agentObjects.Count;
            }
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

        public TmbsAgentTargetStrategy DefaultTargetStrategy
        {
            get => defaultTargetStrategy;
            set => defaultTargetStrategy = value;
        }

        public bool DefaultAutoDeliverResources
        {
            get => defaultAutoDeliverResources;
            set => defaultAutoDeliverResources = value;
        }

        public bool TreatUnrecordedTilesAsBlocking
        {
            get => treatUnrecordedTilesAsBlocking;
            set
            {
                treatUnrecordedTilesAsBlocking = value;
                if (_defaultWalkability != null)
                    _defaultWalkability.TreatUnrecordedTargetTilesAsBlocking = value;
            }
        }

        public BoundsInt? WalkableBounds
        {
            get => _walkableBounds;
            set
            {
                _walkableBounds = value;
                if (_defaultWalkability != null)
                    _defaultWalkability.WalkableBounds = value;
            }
        }

        public Func<Vector3Int, bool> CustomWalkablePredicate
        {
            get => _customWalkablePredicate;
            set
            {
                _customWalkablePredicate = value;
                if (_defaultWalkability != null)
                    _defaultWalkability.CustomWalkablePredicate = value;
            }
        }

        public ITmbsGridWalkabilityProvider WalkabilityProvider
        {
            get
            {
                if (_customWalkabilityProvider != null)
                    return _customWalkabilityProvider;

                EnsureWalkabilityInitialized();
                SyncWalkabilitySources();
                return _defaultWalkability;
            }
        }

        public ITmbsGridPathfinder Pathfinder => _pathfinder ?? (_pathfinder = new TmbsGridPathfinder());

        public void SetCustomWalkabilityProvider(ITmbsGridWalkabilityProvider provider) =>
            _customWalkabilityProvider = provider;

        public void SetCustomPathfinder(ITmbsGridPathfinder pathfinder) =>
            _pathfinder = pathfinder;

        public GameObject SpawnAgent(Vector3 worldPosition, GameObject prefabOverride = null, string agentName = null)
        {
            ResolveBuildeableTilemap();
            Transform container = agentsContainer != null ? agentsContainer : transform;
            GameObject prefab = prefabOverride != null ? prefabOverride : defaultAgentPrefab;
            Tilemap targetTilemap = buildeableTilemap != null ? buildeableTilemap.TargetTilemap : null;

            GameObject go;
            bool createdProcedural = false;
            bool hadAgentComponent = false;

            if (prefab != null)
            {
                go = Instantiate(prefab, worldPosition, Quaternion.identity, container);
                if (!string.IsNullOrEmpty(agentName))
                    go.name = agentName;
                hadAgentComponent = go.GetComponent<TmbsBuildAgent>() != null;
            }
            else
            {
                createdProcedural = true;
                _spawnSequence++;
                string finalName = !string.IsNullOrEmpty(agentName) ? agentName : $"TMBS_Agent_{_spawnSequence}";
                go = TmbsAgentVisualFactory.CreateDefaultAgentGameObject(finalName, worldPosition, container, targetTilemap);
            }

            var agent = RegisterAgentInternal(go, applyDefaults: createdProcedural || !hadAgentComponent);
            if (createdProcedural)
                agent.SyncTintWithState = true;

            AgentSpawned?.Invoke(go);
            return go;
        }

        public GameObject SpawnAgentAtCell(Vector3Int cell, GameObject prefabOverride = null, string agentName = null)
        {
            ResolveBuildeableTilemap();
            Vector3 worldPos = buildeableTilemap != null && buildeableTilemap.TargetTilemap != null
                ? buildeableTilemap.TargetTilemap.GetCellCenterWorld(cell)
                : new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z);

            return SpawnAgent(worldPos, prefabOverride, agentName);
        }

        public List<GameObject> SpawnAgents(
            int count,
            Vector3 centerWorldPosition,
            float spreadRadius = 1.5f,
            GameObject prefabOverride = null)
        {
            int clamped = Mathf.Max(0, count);
            var spawned = new List<GameObject>(clamped);
            if (clamped == 0)
                return spawned;

            Tilemap targetTilemap = buildeableTilemap != null ? buildeableTilemap.TargetTilemap : null;

            for (int i = 0; i < clamped; i++)
            {
                Vector3 pos = centerWorldPosition;
                if (clamped > 1 && spreadRadius > 0.01f)
                {
                    float angle = (Mathf.PI * 2f * i) / clamped;
                    pos += new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spreadRadius;
                }

                if (targetTilemap != null)
                {
                    Vector3Int cell = targetTilemap.WorldToCell(pos);
                    if (TryFindNearestWalkableCell(cell, out var walkableCell))
                        pos = targetTilemap.GetCellCenterWorld(walkableCell);
                }

                spawned.Add(SpawnAgent(pos, prefabOverride));
            }

            return spawned;
        }

        public TmbsBuildAgent RegisterAgent(GameObject agentObject)
        {
            if (agentObject == null)
                throw new ArgumentNullException(nameof(agentObject));

            bool hadAgent = agentObject.GetComponent<TmbsBuildAgent>() != null;
            return RegisterAgentInternal(agentObject, applyDefaults: !hadAgent);
        }

        public void RegisterAgent(TmbsBuildAgent agent)
        {
            if (agent == null || agent.gameObject == null)
                return;

            RegisterAgentInternal(agent.gameObject, applyDefaults: false);
        }

        public void UnregisterAgent(GameObject agentObject)
        {
            if (agentObject == null)
                return;

            var agent = agentObject.GetComponent<TmbsBuildAgent>();
            if (agent != null)
            {
                UnregisterAgent(agent);
            }
            else
            {
                _agentObjects.Remove(agentObject);
            }
        }

        public void UnregisterAgent(TmbsBuildAgent agent)
        {
            if (agent == null)
                return;

            ReleaseReservationsFor(agent);
            _agents.Remove(agent);
            if (agent.gameObject != null)
                _agentObjects.Remove(agent.gameObject);
        }

        public bool DespawnAgent(GameObject agentObject)
        {
            if (agentObject == null)
                return false;

            var agent = agentObject.GetComponent<TmbsBuildAgent>();
            if (agent != null)
            {
                agent.ReleaseCurrentJob();
                UnregisterAgent(agent);
            }
            else
            {
                _agentObjects.Remove(agentObject);
            }

            AgentDespawned?.Invoke(agentObject);

            if (Application.isPlaying)
                Destroy(agentObject);
            else
                DestroyImmediate(agentObject);

            return true;
        }

        public bool DespawnLastAgent()
        {
            PruneDestroyedAgents();
            if (_agentObjects.Count == 0)
                return false;

            return DespawnAgent(_agentObjects[_agentObjects.Count - 1]);
        }

        public void DespawnAllAgents()
        {
            PruneDestroyedAgents();
            for (int i = _agentObjects.Count - 1; i >= 0; i--)
            {
                GameObject go = _agentObjects[i];
                if (go != null)
                    DespawnAgent(go);
            }

            _agents.Clear();
            _agentObjects.Clear();
            _reservedCells.Clear();
        }

        public void ApplySettingsToActiveAgents()
        {
            PruneDestroyedAgents();
            for (int i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                if (agent == null)
                    continue;

                agent.UseBuiltInPathfinding = useBuiltInPathfinding;
                agent.AllowDiagonalMovement = allowDiagonalMovement;
                agent.TargetStrategy = defaultTargetStrategy;
                agent.AutoDeliverResources = defaultAutoDeliverResources;
            }
        }

        public bool IsCellReserved(Vector3Int cell)
        {
            if (!_reservedCells.TryGetValue(cell, out var owner))
                return false;

            if (owner == null || !owner.isActiveAndEnabled)
            {
                _reservedCells.Remove(cell);
                return false;
            }

            return true;
        }

        public bool TryGetReservingAgent(Vector3Int cell, out TmbsBuildAgent agent)
        {
            if (_reservedCells.TryGetValue(cell, out agent) && agent != null && agent.isActiveAndEnabled)
                return true;

            if (_reservedCells.ContainsKey(cell))
                _reservedCells.Remove(cell);

            agent = null;
            return false;
        }

        public bool TryReserveCell(Vector3Int cell, TmbsBuildAgent agent)
        {
            if (agent == null)
                return false;

            if (_reservedCells.TryGetValue(cell, out var existing) && existing != null && existing.isActiveAndEnabled && existing != agent)
                return false;

            _reservedCells[cell] = agent;
            return true;
        }

        public void ReleaseReservationsFor(TmbsBuildAgent agent)
        {
            if (agent == null)
                return;

            _staleReservationKeys.Clear();
            foreach (var kv in _reservedCells)
            {
                if (kv.Value == agent || kv.Value == null)
                    _staleReservationKeys.Add(kv.Key);
            }

            for (int i = 0; i < _staleReservationKeys.Count; i++)
                _reservedCells.Remove(_staleReservationKeys[i]);
        }

        public bool TryFindNearestWalkableCell(Vector3Int origin, out Vector3Int result, int maxRadius = 8)
        {
            return TmbsGridPathfinder.TryFindNearestWalkableCell(origin, WalkabilityProvider, out result, maxRadius);
        }

        public bool TryComputePath(
            Vector3Int startCell,
            Vector3Int goalCell,
            List<Vector3Int> pathBuffer,
            bool? allowDiagonalOverride = null)
        {
            bool diag = allowDiagonalOverride ?? allowDiagonalMovement;
            return Pathfinder.TryFindPath(startCell, goalCell, WalkabilityProvider, pathBuffer, diag, maxPathExpandedNodes);
        }

        public bool TryClaimBestJob(
            TmbsBuildAgent agent,
            Vector3Int agentCell,
            List<Vector3Int> pathBuffer,
            out TmbsAgentJob job)
        {
            job = default;
            if (agent == null)
                return false;

            ResolveBuildeableTilemap();
            if (buildeableTilemap == null || buildeableTilemap.PendingWorkApi == null)
                return false;

            PruneDestroyedAgents();
            ReleaseReservationsFor(agent);

            var workApi = buildeableTilemap.PendingWorkApi;
            var pendingCfg = buildeableTilemap.RootConfig != null ? buildeableTilemap.RootConfig.GetPendingConfig() : null;
            ResourceRequirement policy = pendingCfg != null ? pendingCfg.resourcePolicy : ResourceRequirement.None;
            bool allowPartial = pendingCfg == null || pendingCfg.allowPartialCompletion;

            EnsurePlanner();
            bool planned = _planner.TryPlanBestJob(
                agentCell,
                workApi.GetAll(),
                policy,
                agent.AutoDeliverResources,
                allowPartial,
                treatUnrecordedTilesAsBlocking,
                agent.BuildRangeCells,
                agent.UseBuiltInPathfinding,
                agent.AllowDiagonalMovement,
                agent.TargetStrategy,
                WalkabilityProvider,
                Pathfinder,
                cell => _reservedCells.TryGetValue(cell, out var owner) && owner != null && owner.isActiveAndEnabled && owner != agent,
                pathBuffer,
                out job,
                maxPathExpandedNodes);

            if (!planned)
                return false;

            _reservedCells[job.TargetCell] = agent;
            return true;
        }

        public bool TryPlanSpecificCell(
            TmbsBuildAgent agent,
            Vector3Int agentCell,
            Vector3Int targetCell,
            List<Vector3Int> pathBuffer,
            out TmbsAgentJob job)
        {
            job = default;
            if (agent == null)
                return false;

            ResolveBuildeableTilemap();
            if (buildeableTilemap == null || buildeableTilemap.PendingWorkApi == null)
                return false;

            if (_reservedCells.TryGetValue(targetCell, out var owner) && owner != null && owner.isActiveAndEnabled && owner != agent)
                return false;

            var workApi = buildeableTilemap.PendingWorkApi;
            if (!workApi.TryGetCellStatus(targetCell, out var cellStatus))
                return false;

            int orderId = FindOrderIdForCell(workApi.GetAll(), targetCell);
            EnsurePlanner();

            bool planned = _planner.TryPlanSpecificCell(
                agentCell,
                targetCell,
                orderId,
                in cellStatus,
                treatUnrecordedTilesAsBlocking,
                agent.BuildRangeCells,
                agent.UseBuiltInPathfinding,
                agent.AllowDiagonalMovement,
                WalkabilityProvider,
                Pathfinder,
                pathBuffer,
                out job,
                maxPathExpandedNodes);

            if (!planned)
                return false;

            ReleaseReservationsFor(agent);
            _reservedCells[job.TargetCell] = agent;
            return true;
        }

        private TmbsBuildAgent RegisterAgentInternal(GameObject agentObject, bool applyDefaults)
        {
            ResolveBuildeableTilemap();
            var agent = agentObject.GetComponent<TmbsBuildAgent>();
            if (agent == null)
                agent = agentObject.AddComponent<TmbsBuildAgent>();

            if (applyDefaults)
            {
                agent.UseBuiltInPathfinding = useBuiltInPathfinding;
                agent.AllowDiagonalMovement = allowDiagonalMovement;
                agent.TargetStrategy = defaultTargetStrategy;
                agent.MoveSpeed = defaultMoveSpeed;
                agent.BuildRangeCells = defaultBuildRangeCells;
                agent.WorkPerTick = defaultWorkPerTick;
                agent.WorkInterval = defaultWorkInterval;
                agent.AutoDeliverResources = defaultAutoDeliverResources;
                agent.ResourcesPerTick = defaultResourcesPerTick;
            }

            if (!_agents.Contains(agent))
                _agents.Add(agent);
            if (!_agentObjects.Contains(agentObject))
                _agentObjects.Add(agentObject);

            agent.Initialize(buildeableTilemap, this);
            return agent;
        }

        private void Awake()
        {
            ResolveBuildeableTilemap();
            EnsureWalkabilityInitialized();
            SyncWalkabilitySources();
        }

        private void Start()
        {
            if (spawnOnStartCount > 0)
            {
                SpawnAgents(spawnOnStartCount, transform.position + initialSpawnOffset, spawnSpreadRadius);
            }
        }

        private void Update()
        {
            PruneDestroyedAgents();
        }

        private void OnDisable()
        {
            _reservedCells.Clear();
        }

        private void PruneDestroyedAgents()
        {
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                if (_agents[i] == null || _agents[i].gameObject == null)
                    _agents.RemoveAt(i);
            }

            for (int i = _agentObjects.Count - 1; i >= 0; i--)
            {
                if (_agentObjects[i] == null)
                    _agentObjects.RemoveAt(i);
            }

            if (_reservedCells.Count > 0)
            {
                _staleReservationKeys.Clear();
                foreach (var kv in _reservedCells)
                {
                    if (kv.Value == null || !kv.Value.isActiveAndEnabled)
                        _staleReservationKeys.Add(kv.Key);
                }
                for (int i = 0; i < _staleReservationKeys.Count; i++)
                    _reservedCells.Remove(_staleReservationKeys[i]);
            }
        }

        private void ResolveBuildeableTilemap()
        {
            if (buildeableTilemap != null)
                return;

            buildeableTilemap = GetComponent<BuildeableTilemap>();
            if (buildeableTilemap != null)
                return;

#if UNITY_2023_1_OR_NEWER
            buildeableTilemap = FindAnyObjectByType<BuildeableTilemap>();
#else
            buildeableTilemap = FindObjectOfType<BuildeableTilemap>();
#endif
        }

        private void EnsureWalkabilityInitialized()
        {
            if (_defaultWalkability == null)
            {
                _defaultWalkability = new TmbsTilemapWalkability(
                    buildeableTilemap != null ? buildeableTilemap.Metadata : null,
                    buildeableTilemap != null ? buildeableTilemap.TargetTilemap : null,
                    extraObstacleTilemaps,
                    treatUnrecordedTilesAsBlocking,
                    _walkableBounds,
                    _customWalkablePredicate);
            }
        }

        private void SyncWalkabilitySources()
        {
            if (_defaultWalkability == null)
                return;

            _defaultWalkability.SetSources(
                buildeableTilemap != null ? buildeableTilemap.Metadata : null,
                buildeableTilemap != null ? buildeableTilemap.TargetTilemap : null);
            _defaultWalkability.ExtraObstacleTilemaps = extraObstacleTilemaps;
            _defaultWalkability.TreatUnrecordedTargetTilesAsBlocking = treatUnrecordedTilesAsBlocking;
            _defaultWalkability.WalkableBounds = _walkableBounds;
            _defaultWalkability.CustomWalkablePredicate = _customWalkablePredicate;
        }

        private void EnsurePlanner()
        {
            if (_planner == null)
                _planner = new TmbsSmartJobPlanner();
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
