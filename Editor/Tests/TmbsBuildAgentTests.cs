using System.Collections.Generic;
using NUnit.Framework;
using TMBS.Core.Agents;
using TMBS.Core.Events;
using TMBS.Core.Metadata;
using TMBS.Core.Pending;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using TMBS.Unity.Agents;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Editor.Tests
{
    [TestFixture]
    public sealed class TmbsBuildAgentTests
    {
        private sealed class SpyAgentMotor : MonoBehaviour, ITmbsAgentMotor
        {
            public bool SetDestinationCalled;
            public bool StopMovementCalled;
            public Vector3 LastTargetWorldPos;
            public int LastPathCount;
            public bool ForceArrived = true;

            public void SetDestination(Vector3 targetWorldPosition, IReadOnlyList<Vector3Int> cellPath, Tilemap tilemap)
            {
                SetDestinationCalled = true;
                LastTargetWorldPos = targetWorldPosition;
                LastPathCount = cellPath != null ? cellPath.Count : 0;
            }

            public void StopMovement()
            {
                StopMovementCalled = true;
            }

            public bool HasArrived(Vector3 targetWorldPosition, float arrivalTolerance)
            {
                return ForceArrived;
            }
        }

        private readonly List<Object> _createdObjects = new List<Object>();

        private T Track<T>(T obj) where T : Object
        {
            _createdObjects.Add(obj);
            return obj;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }
            _createdObjects.Clear();
        }

        private TmbsBuildableData CreateBuildable(string id, bool blocksMovement, int workCost = 1, int resourceCost = 0, TileBase tile = null)
        {
            var data = Track(ScriptableObject.CreateInstance<TmbsBuildableData>());
            data.id = id;
            data.displayName = id;
            data.blocksMovement = blocksMovement;
            data.workCost = workCost;
            data.resourceCost = resourceCost;
            data.tile = tile != null ? tile : CreateDummyTile(id);
            return data;
        }

        private Tile CreateDummyTile(string name)
        {
            var tile = Track(ScriptableObject.CreateInstance<Tile>());
            tile.name = name;
            return tile;
        }

        private (BuildeableTilemap controller, Tilemap targetTilemap, TmbsRootConfig config) CreatePendingEnvironment(
            bool allowPartialCompletion = true,
            ResourceRequirement resourcePolicy = ResourceRequirement.None)
        {
            var gridGo = Track(new GameObject("TestGrid", typeof(Grid)));
            var targetGo = Track(new GameObject("TargetTilemap", typeof(Tilemap), typeof(TilemapRenderer)));
            targetGo.transform.SetParent(gridGo.transform, false);
            var previewGo = Track(new GameObject("PreviewTilemap", typeof(Tilemap), typeof(TilemapRenderer)));
            previewGo.transform.SetParent(gridGo.transform, false);

            var targetTilemap = targetGo.GetComponent<Tilemap>();
            var previewTilemap = previewGo.GetComponent<Tilemap>();

            var rootConfig = Track(ScriptableObject.CreateInstance<TmbsRootConfig>());
            rootConfig.executionMode = ExecutionMode.Pending;
            rootConfig.input.mode = TmbsInputMode.None;
            rootConfig.debugMenu.enabled = false;
            rootConfig.pendingDebug.enabled = false;
            rootConfig.pendingDebug.renderDebugTiles = false;
            rootConfig.pendingDebug.GetVisualDebugConfig().enabled = false;
            rootConfig.pending.allowPartialCompletion = allowPartialCompletion;
            rootConfig.pending.resourcePolicy = resourcePolicy;
            rootConfig.validators.Clear();

            var controllerGo = Track(new GameObject("BuildeableController"));
            var controller = controllerGo.AddComponent<BuildeableTilemap>();
            SetPrivateField(controller, "rootConfig", rootConfig);
            SetPrivateField(controller, "targetTilemap", targetTilemap);
            SetPrivateField(controller, "previewTilemap", previewTilemap);

            InvokePrivateMethod(controller, "OnEnable");
            return (controller, targetTilemap, rootConfig);
        }

        [Test]
        public void Pathfinder_RoutesAroundBlockingMetadataTiles_AndPreventsDiagonalCornerCutting()
        {
            var metadata = new MetadataStore(128);
            var wallData = CreateBuildable("wall", blocksMovement: true);
            var floorData = CreateBuildable("floor", blocksMovement: false);

            // Vertical wall blocking x=0 from y=-1 to y=1; floor at (0,2)
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int wallCell = new Vector3Int(0, y, 0);
                metadata.Set(new BuildRecord(wallData.tile, wallCell, BuildState.Completed, wallData));
            }
            metadata.Set(new BuildRecord(floorData.tile, new Vector3Int(0, 2, 0), BuildState.Completed, floorData));

            var walkability = new TmbsTilemapWalkability(metadata, null);
            var pathfinder = new TmbsGridPathfinder();
            var path = new List<Vector3Int>();

            bool found4Way = pathfinder.TryFindPath(
                new Vector3Int(-1, 0, 0),
                new Vector3Int(1, 0, 0),
                walkability,
                path,
                allowDiagonal: false);

            Assert.IsTrue(found4Way, "4-way A* should route over the non-blocking floor cell at (0,2).");
            Assert.IsTrue(path.Contains(new Vector3Int(0, 2, 0)), "Path should pass through walkable floor at (0,2).");
            for (int i = 0; i < path.Count; i++)
                Assert.IsTrue(walkability.IsWalkable(path[i]), $"Path cell {path[i]} must be walkable.");

            // Test diagonal corner-cutting prevention: wall at (1,0) and (0,1) block direct step from (0,0) to (1,1).
            var cornerMeta = new MetadataStore(32);
            cornerMeta.Set(new BuildRecord(wallData.tile, new Vector3Int(1, 0, 0), BuildState.Completed, wallData));
            cornerMeta.Set(new BuildRecord(wallData.tile, new Vector3Int(0, 1, 0), BuildState.Completed, wallData));
            var cornerWalk = new TmbsTilemapWalkability(cornerMeta, null);

            bool found8Way = pathfinder.TryFindPath(
                new Vector3Int(0, 0, 0),
                new Vector3Int(1, 1, 0),
                cornerWalk,
                path,
                allowDiagonal: true);

            Assert.IsTrue(found8Way);
            Assert.Greater(path.Count, 2, "8-way A* must detour around blocked corners instead of squeezing diagonally through (1,0)/(0,1).");
        }

        [Test]
        public void Pathfinder_EnclosedGoal_RespectsMaxExpandedNodes_AndReturnsFalse()
        {
            var metadata = new MetadataStore(64);
            var wallData = CreateBuildable("wall", blocksMovement: true);

            // Seal cell (0,0) and a ring around it at radius 2 so start at (-5,0) is outside.
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    metadata.Set(new BuildRecord(wallData.tile, cell, BuildState.Completed, wallData));
                }
            }

            var walkability = new TmbsTilemapWalkability(metadata, null);
            var pathfinder = new TmbsGridPathfinder();
            var path = new List<Vector3Int>();

            bool found = pathfinder.TryFindPath(
                new Vector3Int(-3, 0, 0),
                new Vector3Int(0, 0, 0),
                walkability,
                path,
                allowDiagonal: false,
                maxExpandedNodes: 64);

            Assert.IsFalse(found);
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void SmartJobPlanner_Solid3x3Block_PrioritizesInteriorBeforePerimeter_AndAvoidsSealingPendingCell()
        {
            var metadata = new MetadataStore(64);
            var wallData = CreateBuildable("wall", blocksMovement: true);

            var cells = new List<PendingCell>();
            for (int x = 0; x <= 2; x++)
            {
                for (int y = 0; y <= 2; y++)
                {
                    cells.Add(new PendingCell(new Vector3Int(x, y, 0), wallData.tile, wallData, 1, 0, 0, 0));
                }
            }

            var order = new PendingOrder("test", new BoundsInt(0, 0, 0, 3, 3, 1), cells.ToArray(), PendingState.ReadyToBuild);
            new PendingStore().Add(order);

            var planner = new TmbsSmartJobPlanner();
            var pathfinder = new TmbsGridPathfinder();
            var walkability = new TmbsTilemapWalkability(metadata, null);
            var path = new List<Vector3Int>();

            bool planned = planner.TryPlanBestJob(
                agentCell: new Vector3Int(-1, 1, 0),
                orders: new[] { order },
                resourcePolicy: ResourceRequirement.None,
                canDeliverResources: true,
                allowPartialCompletion: true,
                treatUnrecordedTilesAsBlocking: false,
                buildRangeCells: 1.5f,
                usePathfinding: true,
                allowDiagonal: false,
                strategy: TmbsAgentTargetStrategy.SmartReachable,
                walkability: walkability,
                pathfinder: pathfinder,
                isCellReserved: null,
                pathBuffer: path,
                out var firstJob);

            Assert.IsTrue(planned);
            Assert.AreEqual(new Vector3Int(1, 1, 0), firstJob.TargetCell,
                "SmartReachable should build interior center (1,1) first before outer cells wall it off.");

            // Anti-seal test: (1,1) has 3 completed walls around it and only door (1,0) open.
            // (1,0) and (1,1) are both pending walls -> planner must build (1,1) BEFORE sealing doorway (1,0).
            var trapMeta = new MetadataStore(64);
            trapMeta.Set(new BuildRecord(wallData.tile, new Vector3Int(0, 1, 0), BuildState.Completed, wallData));
            trapMeta.Set(new BuildRecord(wallData.tile, new Vector3Int(2, 1, 0), BuildState.Completed, wallData));
            trapMeta.Set(new BuildRecord(wallData.tile, new Vector3Int(1, 2, 0), BuildState.Completed, wallData));

            var doorwayOrder = new PendingOrder(
                "test",
                new BoundsInt(1, 0, 0, 1, 2, 1),
                new[]
                {
                    new PendingCell(new Vector3Int(1, 0, 0), wallData.tile, wallData, 1, 0, 0, 0),
                    new PendingCell(new Vector3Int(1, 1, 0), wallData.tile, wallData, 1, 0, 0, 0)
                },
                PendingState.ReadyToBuild);
            new PendingStore().Add(doorwayOrder);

            var trapWalkability = new TmbsTilemapWalkability(trapMeta, null);
            bool plannedTrap = planner.TryPlanBestJob(
                agentCell: new Vector3Int(1, -2, 0),
                orders: new[] { doorwayOrder },
                resourcePolicy: ResourceRequirement.None,
                canDeliverResources: true,
                allowPartialCompletion: true,
                treatUnrecordedTilesAsBlocking: false,
                buildRangeCells: 1.5f,
                usePathfinding: true,
                allowDiagonal: false,
                strategy: TmbsAgentTargetStrategy.SmartReachable,
                walkability: trapWalkability,
                pathfinder: pathfinder,
                isCellReserved: null,
                pathBuffer: path,
                out var doorwayChoice);

            Assert.IsTrue(plannedTrap);
            Assert.AreEqual(new Vector3Int(1, 1, 0), doorwayChoice.TargetCell,
                "SmartReachable must not seal doorway (1,0) while inner pending cell (1,1) still needs construction.");
        }

        [Test]
        public void AgentManager_MultiAgentReservations_NeverAssignSameCellTwice()
        {
            var (controller, _, _) = CreatePendingEnvironment();
            var wallData = CreateBuildable("wall", blocksMovement: false);

            var cells = new[]
            {
                new PendingCell(new Vector3Int(2, 0, 0), wallData.tile, wallData, 2, 0, 0, 0),
                new PendingCell(new Vector3Int(4, 0, 0), wallData.tile, wallData, 2, 0, 0, 0)
            };
            var order = new PendingOrder(controller.InstanceId, new BoundsInt(2, 0, 0, 3, 1, 1), cells, PendingState.ReadyToBuild);
            controller.PendingStore.Add(order);

            var managerGo = Track(new GameObject("AgentManager"));
            var manager = managerGo.AddComponent<TmbsBuildAgentManager>();
            manager.BuildeableTilemap = controller;

            GameObject goA = manager.SpawnAgentAtCell(new Vector3Int(0, 0, 0), agentName: "WorkerA");
            GameObject goB = manager.SpawnAgentAtCell(new Vector3Int(0, 1, 0), agentName: "WorkerB");
            Track(goA);
            Track(goB);

            var agentA = goA.GetComponent<TmbsBuildAgent>();
            var agentB = goB.GetComponent<TmbsBuildAgent>();

            agentA.TickAgent(0.5f);
            agentB.TickAgent(0.5f);

            Assert.IsTrue(agentA.HasActiveJob);
            Assert.IsTrue(agentB.HasActiveJob);
            Assert.AreNotEqual(agentA.CurrentJob.TargetCell, agentB.CurrentJob.TargetCell,
                "Two active agents must reserve distinct pending cells.");

            Vector3Int reservedByA = agentA.CurrentJob.TargetCell;
            manager.DespawnAgent(goA);
            Assert.IsFalse(manager.IsCellReserved(reservedByA),
                "Despawning an agent must immediately release its reserved cell.");
            Assert.AreEqual(1, manager.AgentCount);
        }

        [Test]
        public void AgentManager_SpawnRegisterAndDespawn_WorksWithAnyGameObject()
        {
            var (controller, _, _) = CreatePendingEnvironment();
            var managerGo = Track(new GameObject("AgentManager"));
            var manager = managerGo.AddComponent<TmbsBuildAgentManager>();
            manager.BuildeableTilemap = controller;

            // 1. Procedural debug GameObject spawn (prefab == null)
            GameObject proceduralGo = manager.SpawnAgent(Vector3.zero);
            Track(proceduralGo);
            Assert.IsNotNull(proceduralGo);
            Assert.IsNotNull(proceduralGo.GetComponent<SpriteRenderer>(), "Procedural agent should have a SpriteRenderer.");
            Assert.IsNotNull(proceduralGo.GetComponent<TmbsBuildAgent>(), "Procedural agent should have TmbsBuildAgent.");

            // 2. Arbitrary plain GameObject prefab without TmbsBuildAgent
            var plainPrefab = Track(new GameObject("CustomNpcPrefab"));
            GameObject spawnedCustom = manager.SpawnAgent(new Vector3(3f, 0f, 0f), plainPrefab, "CustomWorker");
            Track(spawnedCustom);
            Assert.AreEqual("CustomWorker", spawnedCustom.name);
            Assert.IsNotNull(spawnedCustom.GetComponent<TmbsBuildAgent>(), "SpawnAgent should auto-attach TmbsBuildAgent to plain prefabs.");

            // 3. Register an arbitrary existing scene GameObject directly
            var existingPlainGo = Track(new GameObject("ExistingHero"));
            TmbsBuildAgent attached = manager.RegisterAgent(existingPlainGo);
            Assert.IsNotNull(attached);
            Assert.AreEqual(existingPlainGo, attached.AgentGameObject);
            Assert.AreEqual(3, manager.AgentCount);
            Assert.AreEqual(3, manager.ActiveAgentObjects.Count);

            manager.DespawnAllAgents();
            Assert.AreEqual(0, manager.AgentCount);
        }

        [Test]
        public void BuildAgent_StepsAsideDeliversResourcesAndCompletesCell_WithoutAutoDebugBuilder()
        {
            var (controller, targetTilemap, _) = CreatePendingEnvironment(
                allowPartialCompletion: true,
                resourcePolicy: ResourceRequirement.RequiredBeforeWork);

            Assert.IsNull(controller.PendingDebugBuilder, "Automatic PendingDebugBuilder must stay null by default.");

            var wallData = CreateBuildable("StoneWall", blocksMovement: true, workCost: 2, resourceCost: 2);
            var cellPos = new Vector3Int(0, 0, 0);
            var order = new PendingOrder(
                controller.InstanceId,
                new BoundsInt(cellPos, Vector3Int.one),
                new[] { new PendingCell(cellPos, wallData.tile, wallData, 2, 0, 2, 0) },
                PendingState.WaitingForResources);
            controller.PendingStore.Add(order);

            var managerGo = Track(new GameObject("AgentManager"));
            var manager = managerGo.AddComponent<TmbsBuildAgentManager>();
            manager.BuildeableTilemap = controller;

            // Spawn agent directly ON TOP of the blocking pending cell (0,0) to verify step-aside!
            GameObject agentGo = manager.SpawnAgentAtCell(cellPos);
            Track(agentGo);

            var agent = agentGo.GetComponent<TmbsBuildAgent>();
            agent.MoveSpeed = 10f;
            agent.BuildRangeCells = 1.5f;
            agent.WorkInterval = 0.05f;
            agent.WorkPerTick = 1;
            agent.ResourcesPerTick = 2;
            agent.AutoDeliverResources = true;

            for (int i = 0; i < 60; i++)
            {
                agent.TickAgent(0.1f);
                if (targetTilemap.GetTile(cellPos) == wallData.tile && !agent.HasActiveJob)
                    break;
            }

            Assert.AreEqual(wallData.tile, targetTilemap.GetTile(cellPos),
                "Agent should step aside, deliver resources, apply work, and place tile on targetTilemap.");
            Assert.IsTrue(controller.TryGetBuildRecord(cellPos, out var rec),
                "MetadataStore must contain completed BuildRecord.");
            Assert.AreEqual(BuildState.Completed, rec.State);
            Assert.AreEqual(wallData, rec.BuildableData);
            Assert.AreNotEqual(cellPos, targetTilemap.WorldToCell(agentGo.transform.position),
                "Agent must have stepped off the blocking cell before completing it.");
            Assert.IsFalse(agent.HasActiveJob);
            Assert.AreEqual(TmbsAgentState.Idle, agent.State);
        }

        [Test]
        public void BuildAgent_AllowPartialCompletionFalse_CompletesWholeMultiCellOrderCleanly()
        {
            var (controller, targetTilemap, _) = CreatePendingEnvironment(allowPartialCompletion: false);
            var floorData = CreateBuildable("Floor", blocksMovement: false, workCost: 1, resourceCost: 0);

            var c1 = new Vector3Int(1, 0, 0);
            var c2 = new Vector3Int(2, 0, 0);
            var order = new PendingOrder(
                controller.InstanceId,
                new BoundsInt(1, 0, 0, 2, 1, 1),
                new[]
                {
                    new PendingCell(c1, floorData.tile, floorData, 1, 0, 0, 0),
                    new PendingCell(c2, floorData.tile, floorData, 1, 0, 0, 0)
                },
                PendingState.ReadyToBuild);
            controller.PendingStore.Add(order);

            var managerGo = Track(new GameObject("AgentManager"));
            var manager = managerGo.AddComponent<TmbsBuildAgentManager>();
            manager.BuildeableTilemap = controller;

            GameObject agentGo = manager.SpawnAgentAtCell(new Vector3Int(0, 0, 0));
            Track(agentGo);

            var agent = agentGo.GetComponent<TmbsBuildAgent>();
            agent.MoveSpeed = 20f;
            agent.WorkInterval = 0.05f;

            for (int i = 0; i < 80; i++)
            {
                agent.TickAgent(0.1f);
                if (controller.PendingWorkApi.GetAll().Count == 0)
                    break;
            }

            Assert.AreEqual(0, controller.PendingWorkApi.GetAll().Count,
                "Multi-cell order should complete and leave pending queue without deadlocking on the first finished cell.");
            Assert.AreEqual(floorData.tile, targetTilemap.GetTile(c1));
            Assert.AreEqual(floorData.tile, targetTilemap.GetTile(c2));
        }

        [Test]
        public void BuildAgent_CustomMotorInterface_DelegatesMovementInsteadOfBuiltInTransform()
        {
            var (controller, targetTilemap, _) = CreatePendingEnvironment();
            var floorData = CreateBuildable("Floor", blocksMovement: false, workCost: 1, resourceCost: 0);

            var targetCell = new Vector3Int(4, 0, 0);
            var order = new PendingOrder(
                controller.InstanceId,
                new BoundsInt(targetCell, Vector3Int.one),
                new[] { new PendingCell(targetCell, floorData.tile, floorData, 1, 0, 0, 0) },
                PendingState.ReadyToBuild);
            controller.PendingStore.Add(order);

            var customGo = Track(new GameObject("NavMeshLikeUnit"));
            customGo.transform.position = targetTilemap.GetCellCenterWorld(Vector3Int.zero);
            var motor = customGo.AddComponent<SpyAgentMotor>();
            motor.ForceArrived = false;

            var agent = TmbsBuildAgent.AttachTo(customGo, controller);
            agent.SetCustomMotor(motor);

            Vector3 initialPos = customGo.transform.position;
            agent.TickAgent(0.1f); // claim job
            agent.TickAgent(0.1f); // drive movement tick while HasArrived == false

            Assert.IsTrue(agent.HasActiveJob);
            Assert.IsTrue(motor.SetDestinationCalled, "Custom ITmbsAgentMotor.SetDestination must be called.");
            Assert.Greater(motor.LastPathCount, 0, "Computed path should be passed to custom motor.");
            Assert.AreEqual(initialPos, customGo.transform.position,
                "Built-in transform movement must not overwrite position when a custom ITmbsAgentMotor is active.");

            // Simulate custom motor arriving and verifying work finishes.
            motor.ForceArrived = true;
            agent.WorkInterval = 0.05f;
            for (int i = 0; i < 10; i++)
            {
                agent.TickAgent(0.1f);
                if (!agent.HasActiveJob)
                    break;
            }

            Assert.IsTrue(motor.StopMovementCalled);
            Assert.AreEqual(floorData.tile, targetTilemap.GetTile(targetCell));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
