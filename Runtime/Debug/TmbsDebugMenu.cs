using System;
using System.Collections.Generic;
using TMBS.Core.Agents;
using TMBS.Core.Pending;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using TMBS.Unity.Agents;
using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TMBS.Runtime.Debugging
{
    /// <summary>IMGUI overlay for inspecting and stepping TMBS pending construction orders.</summary>
    [DisallowMultipleComponent]
    public sealed class TmbsDebugMenu : MonoBehaviour
    {
        [Header("Target TMBS Reference")]
        [Tooltip("The BuildeableTilemap instance to monitor and control. Auto-discovered if null.")]
        [SerializeField] private BuildeableTilemap buildeableTilemap;

        [Header("Runtime State")]
        [SerializeField] private bool isOpen = false;

        private const int WindowId = 1001;
        private Rect _windowRect = new Rect(24, 24, 540, 720);
        private Vector2 _windowScrollPos;
        private Vector2 _scrollPos;
        private bool _isStylesInitialized;
        private bool _showVisualDebugFoldout = true;
        private bool _showAgentsFoldout = true;
        private bool _showLegacyAutoWorker = false;

        private TmbsBuildAgentManager _agentManager;
        private TmbsPendingVisualDebugger _visualDebugger;

        // Deferred IMGUI mutations so Layout and MouseUp/Repaint always see identical control counts
        private int _pendingSpawnCount;
        private bool _pendingDespawnLast;
        private bool _pendingClearAll;
        private GameObject _pendingDespawnTarget;
        private bool _pendingAttachVisualDebugger;

        // Auto-ticker state
        private bool _autoTickerEnabled = false;
        private float _tickerTimer = 0f;
        private float _tickerInterval = 0.2f;
        private int _manualWorkAmount = 1;
        private int _manualResourceAmount = 1;
        private bool _tickerDeliverResources = true;
        private bool _pauseSimulation = false;

        private PendingDebugBuilder _autoWorkerBuilder;
        private TmbsPendingDebugConfig _autoWorkerConfig;
        private IPendingWorkApi _autoWorkerWorkApi;
        private ResourceRequirement _autoWorkerPolicy;

        // GUI Styles
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _metricLabelStyle;
        private GUIStyle _metricValueStyle;
        private GUIStyle _badgeReadyStyle;
        private GUIStyle _badgeWaitingStyle;
        private GUIStyle _badgeProgressStyle;
        private GUIStyle _badgeOtherStyle;
        private GUIStyle _orderBoxStyle;
        private GUIStyle _progressBarBgStyle;
        private GUIStyle _progressBarFillStyle;
        private GUIStyle _modeStatusStyle;
        private GUIStyle _orderHeaderStyle;

        public static bool IsPointerOverAnyMenu { get; private set; }

        public bool IsOpen
        {
            get => isOpen;
            set => isOpen = value;
        }

        public void Open() => isOpen = true;

        public void Close()
        {
            if (_pauseSimulation)
            {
                Time.timeScale = 1f;
                _pauseSimulation = false;
            }
            isOpen = false;
            IsPointerOverAnyMenu = false;
        }

        public void Toggle()
        {
            if (isOpen)
                Close();
            else
                Open();
        }

        private void OnDisable()
        {
            IsPointerOverAnyMenu = false;
            _autoWorkerBuilder = null;
            _autoWorkerWorkApi = null;
            if (_pauseSimulation)
            {
                Time.timeScale = 1f;
                _pauseSimulation = false;
            }
        }

        private void OnDestroy()
        {
            IsPointerOverAnyMenu = false;
            if (_pauseSimulation)
            {
                Time.timeScale = 1f;
                _pauseSimulation = false;
            }
        }

        private void Awake()
        {
            TMBS.Unity.Input.UiFocusGuard.IsPointerBlockedByOverlay = () => IsPointerOverAnyMenu;
            ResolveBuildeableTilemap();
            ResolveSupportComponents();
            SyncWithConfig();
        }

        private void OnEnable()
        {
            TMBS.Unity.Input.UiFocusGuard.IsPointerBlockedByOverlay = () => IsPointerOverAnyMenu;
            ResolveBuildeableTilemap();
            ResolveSupportComponents();
            SyncWithConfig();
        }

        private void ResolveSupportComponents()
        {
            if (_agentManager == null)
            {
                if (buildeableTilemap != null)
                    _agentManager = buildeableTilemap.GetComponent<TmbsBuildAgentManager>();
                if (_agentManager == null)
                    _agentManager = GetComponent<TmbsBuildAgentManager>();
                if (_agentManager == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _agentManager = FindAnyObjectByType<TmbsBuildAgentManager>();
#else
                    _agentManager = FindObjectOfType<TmbsBuildAgentManager>();
#endif
                }
            }

            if (_visualDebugger == null)
            {
                if (buildeableTilemap != null)
                    _visualDebugger = buildeableTilemap.GetComponent<TmbsPendingVisualDebugger>();
                if (_visualDebugger == null)
                    _visualDebugger = GetComponent<TmbsPendingVisualDebugger>();
                if (_visualDebugger == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _visualDebugger = FindAnyObjectByType<TmbsPendingVisualDebugger>();
#else
                    _visualDebugger = FindObjectOfType<TmbsPendingVisualDebugger>();
#endif
                }
            }
        }

        private TmbsBuildAgentManager EnsureAgentManager()
        {
            ResolveBuildeableTilemap();
            ResolveSupportComponents();

            if (_agentManager != null)
            {
                if (buildeableTilemap != null && _agentManager.BuildeableTilemap == null)
                    _agentManager.BuildeableTilemap = buildeableTilemap;
                return _agentManager;
            }

            GameObject host = buildeableTilemap != null ? buildeableTilemap.gameObject : gameObject;
            _agentManager = host.AddComponent<TmbsBuildAgentManager>();
            _agentManager.BuildeableTilemap = buildeableTilemap;
            return _agentManager;
        }

        private TmbsPendingVisualDebugger EnsureVisualDebugger()
        {
            ResolveBuildeableTilemap();
            ResolveSupportComponents();

            if (_visualDebugger != null)
            {
                if (buildeableTilemap != null && _visualDebugger.TargetBuildeableTilemap == null)
                    _visualDebugger.TargetBuildeableTilemap = buildeableTilemap;
                if (_agentManager != null && _visualDebugger.AgentManager == null)
                    _visualDebugger.AgentManager = _agentManager;
                return _visualDebugger;
            }

            GameObject host = buildeableTilemap != null ? buildeableTilemap.gameObject : gameObject;
            _visualDebugger = host.AddComponent<TmbsPendingVisualDebugger>();
            _visualDebugger.TargetBuildeableTilemap = buildeableTilemap;
            _visualDebugger.AgentManager = _agentManager;
            return _visualDebugger;
        }

        private void ResolveBuildeableTilemap()
        {
            if (buildeableTilemap != null) return;

            buildeableTilemap = GetComponent<BuildeableTilemap>();
            if (buildeableTilemap != null) return;

#if UNITY_2023_1_OR_NEWER
            buildeableTilemap = FindAnyObjectByType<BuildeableTilemap>();
#else
            buildeableTilemap = FindObjectOfType<BuildeableTilemap>();
#endif
        }

        private TmbsDebugMenuConfig GetConfig()
        {
            if (buildeableTilemap != null && buildeableTilemap.RootConfig != null)
            {
                return buildeableTilemap.RootConfig.GetDebugMenuConfig();
            }
            return null;
        }

        private void SyncWithConfig()
        {
            var cfg = GetConfig();
            if (cfg != null)
            {
                _tickerInterval = cfg.autoTickInterval;
                _manualWorkAmount = cfg.defaultWorkIncrement;
                _manualResourceAmount = cfg.defaultResourceIncrement;
            }
        }

        private void Update()
        {
            UpdatePointerOverState();

            var cfg = GetConfig();
            if (cfg == null || !cfg.enabled)
            {
                IsPointerOverAnyMenu = false;
                return;
            }

            HandleInputShortcut(cfg);

            if (_autoTickerEnabled)
            {
                TickAutoWorker();
            }
        }

        private void UpdatePointerOverState()
        {
            if (!isOpen)
            {
                IsPointerOverAnyMenu = false;
                return;
            }

            var cfg = GetConfig();
            if (cfg == null || !cfg.enabled)
            {
                IsPointerOverAnyMenu = false;
                return;
            }

            Vector2 screenPos = GetPointerScreenPosition();
            Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
            IsPointerOverAnyMenu = _windowRect.Contains(guiPos);
        }

        private static Vector2 GetPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Pointer.current != null)
            {
                return Pointer.current.position.ReadValue();
            }
#endif
            try
            {
                return Input.mousePosition;
            }
            catch (InvalidOperationException)
            {
                return Vector2.zero;
            }
        }

        private void HandleInputShortcut(TmbsDebugMenuConfig cfg)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                bool ctrlHeld = !cfg.requireControl ||
                                keyboard.ctrlKey.isPressed ||
                                keyboard.leftCtrlKey.isPressed ||
                                keyboard.rightCtrlKey.isPressed ||
                                (keyboard.leftMetaKey != null && keyboard.leftMetaKey.isPressed) ||
                                (keyboard.rightMetaKey != null && keyboard.rightMetaKey.isPressed);

                bool toggleFired = false;
                switch (cfg.toggleKey)
                {
                    case KeyCode.Slash:
                        toggleFired = keyboard.slashKey.wasPressedThisFrame;
                        break;
                    case KeyCode.BackQuote:
                        toggleFired = keyboard.backquoteKey.wasPressedThisFrame;
                        break;
                    case KeyCode.F1:
                        toggleFired = keyboard.f1Key.wasPressedThisFrame;
                        break;
                    case KeyCode.F12:
                        toggleFired = keyboard.f12Key.wasPressedThisFrame;
                        break;
                    default:
                        if (Enum.TryParse<Key>(cfg.toggleKey.ToString(), true, out var key))
                        {
                            toggleFired = keyboard[key].wasPressedThisFrame;
                        }
                        break;
                }

                if (ctrlHeld && toggleFired)
                {
                    Toggle();
                    return;
                }
            }
#endif

            try
            {
                bool legacyCtrl = !cfg.requireControl ||
                                  Input.GetKey(KeyCode.LeftControl) ||
                                  Input.GetKey(KeyCode.RightControl) ||
                                  Input.GetKey(KeyCode.LeftCommand) ||
                                  Input.GetKey(KeyCode.RightCommand);

                if (legacyCtrl && Input.GetKeyDown(cfg.toggleKey))
                {
                    Toggle();
                }
            }
            catch (InvalidOperationException)
            {
                // Ignored when Unity's new Input System is exclusively active
            }
        }

        private ResourceRequirement GetResourcePolicy()
        {
            if (buildeableTilemap != null && buildeableTilemap.RootConfig != null)
            {
                var cfg = buildeableTilemap.RootConfig.GetPendingConfig();
                if (cfg != null) return cfg.resourcePolicy;
            }
            return ResourceRequirement.None;
        }

        private void TickAutoWorker()
        {
            if (_pauseSimulation || Time.timeScale <= 0f)
                return;

            _tickerTimer += Time.unscaledDeltaTime;
            if (_tickerTimer < _tickerInterval)
                return;

            _tickerTimer = 0f;

            if (buildeableTilemap == null) return;
            var workApi = buildeableTilemap.PendingWorkApi;
            if (workApi == null) return;

            var policy = GetResourcePolicy();
            var rootPendingDebug = buildeableTilemap.RootConfig != null
                ? buildeableTilemap.RootConfig.GetPendingDebugConfig()
                : null;

            if (_autoWorkerConfig == null)
            {
                _autoWorkerConfig = new TmbsPendingDebugConfig();
            }

            _autoWorkerConfig.enabled = true;
            _autoWorkerConfig.workPerStep = Mathf.Max(1, _manualWorkAmount);
            _autoWorkerConfig.cellsPerStep = rootPendingDebug != null ? Mathf.Max(1, rootPendingDebug.cellsPerStep) : 1;
            _autoWorkerConfig.buildOrder = rootPendingDebug != null ? rootPendingDebug.buildOrder : PendingDebugBuildOrder.ByStoreOrder;
            _autoWorkerConfig.autoDeliverResources = _tickerDeliverResources;

            if (_autoWorkerBuilder == null ||
                !ReferenceEquals(_autoWorkerWorkApi, workApi) ||
                _autoWorkerPolicy != policy)
            {
                _autoWorkerWorkApi = workApi;
                _autoWorkerPolicy = policy;
                _autoWorkerBuilder = new PendingDebugBuilder(workApi, _autoWorkerConfig, policy);
            }

            _autoWorkerBuilder.Step();
        }

        private void OnGUI()
        {
            var cfg = GetConfig();
            if (cfg == null || !cfg.enabled || !isOpen)
            {
                IsPointerOverAnyMenu = false;
                return;
            }

            InitStyles();

            // Responsive sizing based on screen resolution
            float maxW = Mathf.Min(540f, Mathf.Max(320f, Screen.width - 24f));
            float maxH = Mathf.Min(740f, Mathf.Max(240f, Screen.height - 24f));
            _windowRect.width = maxW;
            _windowRect.height = maxH;

            // Clamp window to screen
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Mathf.Max(0, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Mathf.Max(0, Screen.height - _windowRect.height));

            if (Event.current != null)
            {
                IsPointerOverAnyMenu = _windowRect.Contains(Event.current.mousePosition);
            }

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 0.96f);

            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindowContent, "TMBS Debug Menu & Pending Controller");

            GUI.backgroundColor = prevBg;

            ProcessDeferredAgentActions();
        }

        private void ProcessDeferredAgentActions()
        {
            if (_pendingAttachVisualDebugger)
            {
                _pendingAttachVisualDebugger = false;
                var dbg = EnsureVisualDebugger();
                dbg.ActiveConfig.enabled = true;
            }

            if (_pendingClearAll)
            {
                _pendingClearAll = false;
                _agentManager?.DespawnAllAgents();
            }

            if (_pendingDespawnLast)
            {
                _pendingDespawnLast = false;
                _agentManager?.DespawnLastAgent();
            }

            if (_pendingDespawnTarget != null)
            {
                GameObject target = _pendingDespawnTarget;
                _pendingDespawnTarget = null;
                _agentManager?.DespawnAgent(target);
            }

            if (_pendingSpawnCount > 0)
            {
                int count = _pendingSpawnCount;
                _pendingSpawnCount = 0;
                var mgr = EnsureAgentManager();
                if (count == 1)
                    mgr.SpawnAgent(GetCameraSpawnWorldPosition());
                else
                    mgr.SpawnAgents(count, GetCameraSpawnWorldPosition(), 2.0f);
            }
        }

        private void InitStyles()
        {
            if (_isStylesInitialized && _headerStyle != null && _headerStyle.normal != null) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.95f, 1f) }
            };

            _subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.75f, 0.85f, 0.95f) }
            };

            _metricLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.75f, 0.8f) }
            };

            _metricValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _badgeReadyStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 1f, 0.4f) }
            };

            _badgeWaitingStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.7f, 0.2f) }
            };

            _badgeProgressStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.3f, 0.75f, 1f) }
            };

            _badgeOtherStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray }
            };

            _orderBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };

            _progressBarBgStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 2, 2)
            };

            _progressBarFillStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _modeStatusStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontStyle = FontStyle.Bold
            };

            _orderHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            _isStylesInitialized = true;
        }

        private void DrawWindowContent(int windowId)
        {
            // Title Bar Controls
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build & Pending Tools", _headerStyle);
            GUILayout.FlexibleSpace();

            bool prevPause = _pauseSimulation;
            _pauseSimulation = GUILayout.Toggle(_pauseSimulation, "Pause", GUILayout.Width(60));
            if (_pauseSimulation != prevPause)
            {
                Time.timeScale = _pauseSimulation ? 0f : 1f;
            }

            if (GUILayout.Button("X", GUILayout.Width(26), GUILayout.Height(20)))
            {
                Close();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            DrawModeSection();

            GUILayout.Space(6);

            _windowScrollPos = GUILayout.BeginScrollView(_windowScrollPos);

            DrawAgentsSection();

            GUILayout.Space(6);

            DrawVisualDebugSection();

            GUILayout.Space(6);

            DrawMetricsSection();

            GUILayout.Space(6);

            DrawQuickActionsSection();

            GUILayout.Space(6);

            DrawAutoTickerSection();

            GUILayout.Space(6);

            DrawOrdersInspectorSection();

            GUILayout.EndScrollView();

            // Allow dragging from anywhere on header label area without blocking top-right controls
            float dragWidth = Mathf.Max(60f, _windowRect.width - 110f);
            GUI.DragWindow(new Rect(0, 0, dragWidth, 30));
        }

        private void DrawAgentsSection()
        {
            ResolveSupportComponents();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            int agentCount = _agentManager != null ? _agentManager.AgentCount : 0;
            _showAgentsFoldout = GUILayout.Toggle(_showAgentsFoldout, $"Builder Agents ({agentCount})", _subHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (_showAgentsFoldout)
            {
                GUILayout.BeginHorizontal();
                var origBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.25f, 0.72f, 0.95f);
                if (GUILayout.Button("Spawn Agent", GUILayout.Height(22)))
                {
                    _pendingSpawnCount += 1;
                }
                if (GUILayout.Button("Spawn +3", GUILayout.Height(22)))
                {
                    _pendingSpawnCount += 3;
                }
                GUI.backgroundColor = origBg;

                GUI.enabled = agentCount > 0;
                if (GUILayout.Button("Despawn Last", GUILayout.Height(22)))
                {
                    _pendingDespawnLast = true;
                }
                if (GUILayout.Button("Clear All", GUILayout.Height(22)))
                {
                    _pendingClearAll = true;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                if (_agentManager != null)
                {
                    bool changed = false;
                    GUILayout.BeginHorizontal();

                    bool nextPathfinding = GUILayout.Toggle(_agentManager.UseBuiltInPathfinding, "A* Pathfinding");
                    if (nextPathfinding != _agentManager.UseBuiltInPathfinding)
                    {
                        _agentManager.UseBuiltInPathfinding = nextPathfinding;
                        changed = true;
                    }

                    bool nextDiag = GUILayout.Toggle(_agentManager.AllowDiagonalMovement, "Diagonal");
                    if (nextDiag != _agentManager.AllowDiagonalMovement)
                    {
                        _agentManager.AllowDiagonalMovement = nextDiag;
                        changed = true;
                    }

                    bool nextDeliver = GUILayout.Toggle(_agentManager.DefaultAutoDeliverResources, "Auto-Deliver Res");
                    if (nextDeliver != _agentManager.DefaultAutoDeliverResources)
                    {
                        _agentManager.DefaultAutoDeliverResources = nextDeliver;
                        changed = true;
                    }

                    if (GUILayout.Button($"Order: {_agentManager.DefaultTargetStrategy}", GUILayout.Height(20), GUILayout.Width(165)))
                    {
                        int nextEnum = ((int)_agentManager.DefaultTargetStrategy + 1) % 3;
                        _agentManager.DefaultTargetStrategy = (TmbsAgentTargetStrategy)nextEnum;
                        changed = true;
                    }

                    GUILayout.EndHorizontal();

                    if (changed)
                    {
                        _agentManager.ApplySettingsToActiveAgents();
                    }

                    var activeAgents = _agentManager.ActiveAgents;
                    if (activeAgents != null && activeAgents.Count > 0)
                    {
                        for (int i = 0; i < activeAgents.Count; i++)
                        {
                            var agent = activeAgents[i];
                            if (agent == null)
                                continue;

                            GUILayout.BeginHorizontal();
                            GUILayout.Label(agent.gameObject.name, _metricValueStyle, GUILayout.Width(135));
                            GUILayout.Label(agent.State.ToString(), _metricLabelStyle, GUILayout.Width(130));
                            string targetText = agent.HasActiveJob
                                ? $"({agent.CurrentJob.TargetCell.x}, {agent.CurrentJob.TargetCell.y})"
                                : "-";
                            GUILayout.Label($"Target: {targetText}", _metricLabelStyle);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("X", GUILayout.Width(24), GUILayout.Height(18)))
                            {
                                _pendingDespawnTarget = agent.gameObject;
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                else
                {
                    GUILayout.Label("No TmbsBuildAgentManager active yet. Click 'Spawn Agent' to create one.", _metricLabelStyle);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawVisualDebugSection()
        {
            ResolveSupportComponents();

            TmbsPendingVisualDebugConfig visualCfg = null;
            if (buildeableTilemap != null && buildeableTilemap.RootConfig != null)
                visualCfg = buildeableTilemap.RootConfig.GetPendingDebugConfig().GetVisualDebugConfig();
            else if (_visualDebugger != null)
                visualCfg = _visualDebugger.ActiveConfig;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            _showVisualDebugFoldout = GUILayout.Toggle(_showVisualDebugFoldout, "Visual Pending Debug", _subHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (_showVisualDebugFoldout)
            {
                if (visualCfg == null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("No RootConfig visual profile found.", _metricLabelStyle);
                    if (GUILayout.Button("Attach Visual Debugger", GUILayout.Height(20), GUILayout.Width(160)))
                    {
                        _pendingAttachVisualDebugger = true;
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    bool prevMaster = visualCfg.enabled;
                    bool nextMaster = GUILayout.Toggle(prevMaster, "Enable Pending Visual Overlays (Scene + Game View)");
                    if (nextMaster != prevMaster)
                    {
                        visualCfg.enabled = nextMaster;
                        if (nextMaster)
                            _pendingAttachVisualDebugger = true;
                    }

                    GUI.enabled = visualCfg.enabled;
                    GUILayout.BeginHorizontal();
                    visualCfg.showInGameView = GUILayout.Toggle(visualCfg.showInGameView, "Game View");
                    visualCfg.showInSceneView = GUILayout.Toggle(visualCfg.showInSceneView, "Scene Gizmos");
                    visualCfg.showStateOutlines = GUILayout.Toggle(visualCfg.showStateOutlines, "Outlines");
                    visualCfg.showProgressBars = GUILayout.Toggle(visualCfg.showProgressBars, "Progress Bars");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    visualCfg.showCellLabels = GUILayout.Toggle(visualCfg.showCellLabels, "Cell Labels");
                    visualCfg.showAgentPaths = GUILayout.Toggle(visualCfg.showAgentPaths, "Agent Paths");
                    visualCfg.showAgentTargets = GUILayout.Toggle(visualCfg.showAgentTargets, "Agent Targets");
                    visualCfg.showWalkabilityOverlay = GUILayout.Toggle(visualCfg.showWalkabilityOverlay, "Walkability");
                    GUILayout.EndHorizontal();
                    GUI.enabled = true;
                }
            }

            GUILayout.EndVertical();
        }

        private Vector3 GetCameraSpawnWorldPosition()
        {
            Tilemap tilemap = buildeableTilemap != null ? buildeableTilemap.TargetTilemap : null;
            Camera cam = buildeableTilemap != null ? buildeableTilemap.ResolvedCamera : Camera.main;
            if (cam == null)
                cam = Camera.main;

            Vector3 worldPos = tilemap != null ? tilemap.transform.position : Vector3.zero;
            if (cam != null)
            {
                Plane plane = tilemap != null
                    ? new Plane(tilemap.transform.forward, tilemap.transform.position)
                    : new Plane(Vector3.back, Vector3.zero);
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (plane.Raycast(ray, out float enter))
                    worldPos = ray.GetPoint(enter);
                else
                    worldPos = new Vector3(cam.transform.position.x, cam.transform.position.y, worldPos.z);
            }

            if (tilemap != null && _agentManager != null)
            {
                Vector3Int cell = tilemap.WorldToCell(worldPos);
                if (_agentManager.TryFindNearestWalkableCell(cell, out var walkableCell))
                    worldPos = tilemap.GetCellCenterWorld(walkableCell);
                else
                    worldPos = tilemap.GetCellCenterWorld(cell);
            }
            else if (tilemap != null)
            {
                worldPos = tilemap.GetCellCenterWorld(tilemap.WorldToCell(worldPos));
            }

            return worldPos;
        }

        private void DrawModeSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            var currentMode = buildeableTilemap != null && buildeableTilemap.RootConfig != null
                ? buildeableTilemap.RootConfig.executionMode
                : ExecutionMode.Immediate;

            GUILayout.Label("Execution Mode:", _subHeaderStyle, GUILayout.Width(130));

            string modeText = currentMode == ExecutionMode.Immediate
                ? "<color=#55ff88>IMMEDIATE (Direct)</color>"
                : "<color=#55ccff>PENDING (Deferred)</color>";

            GUILayout.Label(modeText, _modeStatusStyle);

            GUILayout.FlexibleSpace();

            string buttonText = currentMode == ExecutionMode.Immediate
                ? "Switch to PENDING"
                : "Switch to IMMEDIATE";

            var origBg = GUI.backgroundColor;
            GUI.backgroundColor = currentMode == ExecutionMode.Immediate
                ? new Color(0.2f, 0.6f, 1f)
                : new Color(0.3f, 0.8f, 0.3f);

            if (GUILayout.Button(buttonText, GUILayout.Height(24), GUILayout.Width(160)))
            {
                if (buildeableTilemap != null)
                {
                    var newMode = currentMode == ExecutionMode.Immediate
                        ? ExecutionMode.Pending
                        : ExecutionMode.Immediate;
                    buildeableTilemap.SwitchExecutionMode(newMode);
                }
            }
            GUI.backgroundColor = origBg;

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawMetricsSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Global Metrics", _subHeaderStyle);

            var workApi = buildeableTilemap != null ? buildeableTilemap.PendingWorkApi : null;
            var orders = workApi != null ? workApi.GetAll() : null;

            int orderCount = orders != null ? orders.Count : 0;
            int totalCells = 0;
            int completedCells = 0;
            int waitingCells = 0;
            int readyCells = 0;
            int inProgressCells = 0;
            int totalWorkDone = 0;
            int totalWorkReq = 0;
            int totalResDelivered = 0;
            int totalResReq = 0;

            if (orders != null)
            {
                for (int i = 0; i < orders.Count; i++)
                {
                    var order = orders[i];
                    if (order == null || order.Cells == null) continue;

                    for (int c = 0; c < order.Cells.Length; c++)
                    {
                        var cell = order.Cells[c];
                        totalCells++;
                        totalWorkDone += cell.WorkDone;
                        totalWorkReq += cell.WorkRequired;
                        totalResDelivered += cell.ResourceDelivered;
                        totalResReq += cell.ResourceRequired;

                        if (cell.IsCompleted)
                        {
                            completedCells++;
                        }
                        else if (!cell.IsReadyForWork)
                        {
                            waitingCells++;
                        }
                        else if (!cell.IsWorkComplete)
                        {
                            if (cell.WorkDone > 0)
                                inProgressCells++;
                            else
                                readyCells++;
                        }
                        else
                        {
                            readyCells++;
                        }
                    }
                }
            }

            GUILayout.BeginHorizontal();
            DrawMetricItem("Orders:", orderCount.ToString());
            DrawMetricItem("Cells:", totalCells.ToString());
            DrawMetricItem("Completed:", completedCells.ToString());
            DrawMetricItem("Waiting:", waitingCells.ToString());
            DrawMetricItem("Ready:", readyCells.ToString());
            DrawMetricItem("In Progress:", inProgressCells.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawMetricItem("Work Progress:", $"{totalWorkDone}/{totalWorkReq}");
            DrawMetricItem("Resources:", $"{totalResDelivered}/{totalResReq}");
            float overallProgress = totalWorkReq > 0 ? (float)totalWorkDone / totalWorkReq : 0f;
            DrawMetricItem("Overall:", $"{Mathf.RoundToInt(overallProgress * 100f)}%");
            GUILayout.EndHorizontal();

            DrawProgressBar(overallProgress, 14);

            GUILayout.EndVertical();
        }

        private void DrawMetricItem(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _metricLabelStyle, GUILayout.ExpandWidth(false));
            GUILayout.Label(value, _metricValueStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(8);
            GUILayout.EndHorizontal();
        }

        private void DrawQuickActionsSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Quick Actions", _subHeaderStyle);

            var workApi = buildeableTilemap != null ? buildeableTilemap.PendingWorkApi : null;
            bool hasOrders = workApi != null && workApi.GetAll() != null && workApi.GetAll().Count > 0;

            GUI.enabled = hasOrders;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 Work", GUILayout.Height(24)))
            {
                AdvanceAllWork(1);
            }
            if (_manualWorkAmount != 1 && GUILayout.Button($"+{_manualWorkAmount} Work", GUILayout.Height(24)))
            {
                AdvanceAllWork(_manualWorkAmount);
            }
            if (GUILayout.Button($"+{_manualResourceAmount} Resources", GUILayout.Height(24)))
            {
                DeliverAllNeededResources();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Complete Next Cell", GUILayout.Height(24)))
            {
                CompleteNextCell();
            }
            var origColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.35f);
            if (GUILayout.Button("Complete All", GUILayout.Height(24)))
            {
                CompleteAllPending();
            }
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Cancel All", GUILayout.Height(24)))
            {
                CancelAllPending();
            }
            GUI.backgroundColor = origColor;
            GUILayout.EndHorizontal();

            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private void DrawAutoTickerSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            _showLegacyAutoWorker = GUILayout.Toggle(_showLegacyAutoWorker, "Legacy Auto-Step Debug (Automatic, No Agents)", _subHeaderStyle);
            if (!_showLegacyAutoWorker)
            {
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            _autoTickerEnabled = GUILayout.Toggle(_autoTickerEnabled, "Auto-Step Worker", GUILayout.Width(180));
            _tickerDeliverResources = GUILayout.Toggle(_tickerDeliverResources, "Auto-Deliver Resources");
            GUILayout.EndHorizontal();

            if (_autoTickerEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Interval: {_tickerInterval:F2}s", GUILayout.Width(100));
                _tickerInterval = GUILayout.HorizontalSlider(_tickerInterval, 0.05f, 1.0f);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Work: {_manualWorkAmount}", GUILayout.Width(68));
            if (GUILayout.Button("-", GUILayout.Width(24)))
                _manualWorkAmount = Mathf.Max(1, _manualWorkAmount - 1);
            if (GUILayout.Button("+", GUILayout.Width(24)))
                _manualWorkAmount++;

            GUILayout.Space(12);
            GUILayout.Label($"Res: {_manualResourceAmount}", GUILayout.Width(64));
            if (GUILayout.Button("-", GUILayout.Width(24)))
                _manualResourceAmount = Mathf.Max(1, _manualResourceAmount - 1);
            if (GUILayout.Button("+", GUILayout.Width(24)))
                _manualResourceAmount++;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawOrdersInspectorSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Pending Orders Inspector", _subHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var workApi = buildeableTilemap != null ? buildeableTilemap.PendingWorkApi : null;
            var currentMode = buildeableTilemap != null && buildeableTilemap.RootConfig != null
                ? buildeableTilemap.RootConfig.executionMode
                : ExecutionMode.Immediate;

            if (currentMode == ExecutionMode.Immediate)
            {
                GUILayout.Space(12);
                GUILayout.Label("System is in IMMEDIATE mode.\nTiles are placed directly onto the tilemap without creating pending orders.\nClick 'Switch to PENDING' above to test the deferred work queue.", _metricLabelStyle);
                GUILayout.Space(12);
                GUILayout.EndVertical();
                return;
            }

            var orders = workApi != null ? workApi.GetAll() : null;

            if (orders == null || orders.Count == 0)
            {
                GUILayout.Space(16);
                GUILayout.Label("No active pending orders in the queue.\nSelect a buildable from the palette and drag across the tilemap to create orders.", _metricLabelStyle);
                GUILayout.Space(16);
                GUILayout.EndVertical();
                return;
            }

            float scrollHeight = 210f;
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(scrollHeight));

            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null) continue;

                DrawOrderItem(order, workApi);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawOrderItem(PendingOrder order, IPendingWorkApi workApi)
        {
            GUILayout.BeginVertical(_orderBoxStyle);

            GUILayout.BeginHorizontal();

            // Order header & State badge
            GUILayout.Label($"Order #{order.Id}", _orderHeaderStyle, GUILayout.Width(75));

            string stateName = order.State.ToString();
            GUIStyle badgeStyle = GetBadgeStyle(order.State);
            GUILayout.Box(stateName, badgeStyle, GUILayout.Width(130), GUILayout.Height(18));

            // Buildable definition name
            string buildableName;
            if (order.PrimaryBuildableData != null)
            {
                buildableName = order.PrimaryBuildableData.displayName;
            }
            else if (order.Cells != null && order.Cells.Length > 0)
            {
                buildableName = order.Cells[0].Tile != null
                    ? order.Cells[0].Tile.name
                    : "Demolition / Erase";
            }
            else
            {
                buildableName = "Tile";
            }
            GUILayout.Label($"[{buildableName}]", _metricValueStyle);

            GUILayout.FlexibleSpace();

            var origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Cancel", GUILayout.Width(75), GUILayout.Height(18)))
            {
                workApi.TryCancel(order.Id);
                GUI.backgroundColor = origBg;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return;
            }
            GUI.backgroundColor = origBg;

            GUILayout.EndHorizontal();

            // Progress bar
            float prog = order.NormalizedProgress;
            DrawProgressBar(prog, 12);

            // Cells list
            var cells = order.Cells;
            if (cells != null)
            {
                for (int c = 0; c < cells.Length; c++)
                {
                    var cell = cells[c];
                    DrawCellRow(cell, workApi, order.Id);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawCellRow(PendingCell cell, IPendingWorkApi workApi, int orderId)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label($"({cell.Cell.x}, {cell.Cell.y})", _metricLabelStyle, GUILayout.Width(65));
            GUILayout.Label($"Work: {cell.WorkDone}/{cell.WorkRequired}", _metricLabelStyle, GUILayout.Width(100));

            if (cell.ResourceRequired > 0)
            {
                GUILayout.Label($"Resources: {cell.ResourceDelivered}/{cell.ResourceRequired}", _metricLabelStyle, GUILayout.Width(110));
            }
            else
            {
                GUILayout.Label("No resources", _metricLabelStyle, GUILayout.Width(110));
            }

            GUILayout.FlexibleSpace();

            if (cell.IsCompleted)
            {
                GUILayout.Box("Completed", _badgeReadyStyle, GUILayout.Width(80), GUILayout.Height(18));
            }
            else
            {
                var policy = GetResourcePolicy();
                if (cell.ResourceDelivered < cell.ResourceRequired)
                {
                    if (GUILayout.Button("+ Resource", GUILayout.Width(78), GUILayout.Height(18)))
                    {
                        int needed = cell.ResourceRequired - cell.ResourceDelivered;
                        workApi.TryDeliverResources(cell.Cell, Mathf.Min(needed, _manualResourceAmount));
                    }
                }
                if (cell.WorkDone < cell.WorkAllowed(policy))
                {
                    if (GUILayout.Button("+ Work", GUILayout.Width(70), GUILayout.Height(18)))
                    {
                        int allowed = cell.WorkAllowed(policy) - cell.WorkDone;
                        int toAdd = Mathf.Min(allowed, _manualWorkAmount);
                        workApi.TryAddWork(cell.Cell, toAdd);
                        if (workApi.TryGetCellStatus(cell.Cell, out var updated) && updated.IsWorkComplete)
                        {
                            workApi.TryCompleteCell(cell.Cell);
                        }
                    }
                }

                if (GUILayout.Button("Complete", GUILayout.Width(70), GUILayout.Height(18)))
                {
                    if (cell.ResourceDelivered < cell.ResourceRequired)
                    {
                        workApi.TryDeliverResources(cell.Cell, cell.ResourceRequired - cell.ResourceDelivered);
                    }
                    int rem = cell.WorkRequired - cell.WorkDone;
                    if (rem > 0)
                    {
                        workApi.TryAddWork(cell.Cell, rem);
                    }
                    workApi.TryCompleteCell(cell.Cell);
                }
            }

            GUILayout.EndHorizontal();
        }

        private GUIStyle GetBadgeStyle(PendingState state)
        {
            switch (state)
            {
                case PendingState.ReadyToBuild:
                    return _badgeReadyStyle;
                case PendingState.WaitingForResources:
                    return _badgeWaitingStyle;
                case PendingState.InProgress:
                    return _badgeProgressStyle;
                default:
                    return _badgeOtherStyle;
            }
        }

        private void DrawProgressBar(float progress, float height)
        {
            progress = Mathf.Clamp01(progress);
            var rect = GUILayoutUtility.GetRect(100, height, GUILayout.ExpandWidth(true));

            // Background
            GUI.Box(rect, GUIContent.none, _progressBarBgStyle);

            if (progress > 0.001f)
            {
                var fillRect = new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * progress, rect.height - 2);
                var origColor = GUI.color;
                GUI.color = Color.Lerp(new Color(1f, 0.6f, 0.2f), new Color(0.2f, 0.9f, 0.4f), progress);
                GUI.Box(fillRect, GUIContent.none, _progressBarFillStyle);
                GUI.color = origColor;
            }
        }

        public void AdvanceAllWork(int amount)
        {
            if (buildeableTilemap == null) return;
            var workApi = buildeableTilemap.PendingWorkApi;
            if (workApi == null) return;

            var orders = workApi.GetAll();
            if (orders == null) return;

            var toComplete = new List<Vector3Int>();
            var policy = GetResourcePolicy();

            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null || order.Cells == null) continue;

                for (int c = 0; c < order.Cells.Length; c++)
                {
                    var cell = order.Cells[c];
                    if (!cell.IsCompleted)
                    {
                        int allowed = cell.WorkAllowed(policy);
                        if (cell.WorkDone < allowed)
                        {
                            int rem = allowed - cell.WorkDone;
                            int toAdd = Mathf.Min(rem, amount);
                            workApi.TryAddWork(cell.Cell, toAdd);
                            if (workApi.TryGetCellStatus(cell.Cell, out var updated) && updated.IsWorkComplete)
                            {
                                toComplete.Add(cell.Cell);
                            }
                        }
                        else if (cell.IsWorkComplete)
                        {
                            toComplete.Add(cell.Cell);
                        }
                    }
                }
            }

            for (int i = 0; i < toComplete.Count; i++)
            {
                workApi.TryCompleteCell(toComplete[i]);
            }
        }

        public void DeliverAllNeededResources()
        {
            if (buildeableTilemap == null) return;
            var workApi = buildeableTilemap.PendingWorkApi;
            if (workApi == null) return;

            var orders = workApi.GetAll();
            if (orders == null) return;

            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null || order.Cells == null) continue;

                for (int c = 0; c < order.Cells.Length; c++)
                {
                    var cell = order.Cells[c];
                    if (!cell.IsCompleted && cell.ResourceDelivered < cell.ResourceRequired)
                    {
                        int needed = cell.ResourceRequired - cell.ResourceDelivered;
                        if (needed > 0)
                        {
                            workApi.TryDeliverResources(cell.Cell, needed);
                        }
                    }
                }
            }
        }

        public void CompleteNextCell()
        {
            if (buildeableTilemap == null) return;
            var workApi = buildeableTilemap.PendingWorkApi;
            if (workApi == null) return;

            var orders = workApi.GetAll();
            if (orders == null || orders.Count == 0) return;

            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null || order.Cells == null) continue;

                for (int c = 0; c < order.Cells.Length; c++)
                {
                    var cell = order.Cells[c];
                    if (!cell.IsCompleted)
                    {
                        if (!cell.IsReadyForWork)
                        {
                            workApi.TryDeliverResources(cell.Cell, cell.ResourceRequired - cell.ResourceDelivered);
                        }
                        int rem = cell.WorkRequired - cell.WorkDone;
                        if (rem > 0)
                        {
                            workApi.TryAddWork(cell.Cell, rem);
                        }
                        workApi.TryCompleteCell(cell.Cell);
                        return;
                    }
                }
            }
        }

        public void CompleteAllPending()
        {
            if (buildeableTilemap == null) return;
            var workApi = buildeableTilemap.PendingWorkApi;
            if (workApi == null) return;

            var orders = workApi.GetAll();
            if (orders == null || orders.Count == 0) return;

            // Collect all cells to avoid collection mutation issues while removing
            var allCells = new List<PendingCell>();
            for (int i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null || order.Cells == null) continue;

                for (int c = 0; c < order.Cells.Length; c++)
                {
                    allCells.Add(order.Cells[c]);
                }
            }

            for (int i = 0; i < allCells.Count; i++)
            {
                var cell = allCells[i];
                if (!cell.IsCompleted)
                {
                    if (!cell.IsReadyForWork)
                    {
                        workApi.TryDeliverResources(cell.Cell, cell.ResourceRequired - cell.ResourceDelivered);
                    }
                    int rem = cell.WorkRequired - cell.WorkDone;
                    if (rem > 0)
                    {
                        workApi.TryAddWork(cell.Cell, rem);
                    }
                    workApi.TryCompleteCell(cell.Cell);
                }
            }
        }

        public void CancelAllPending()
        {
            if (buildeableTilemap == null) return;
            var workApi = buildeableTilemap.PendingWorkApi;
            if (workApi == null) return;

            var orders = workApi.GetAll();
            if (orders == null || orders.Count == 0) return;

            var ids = new List<int>(orders.Count);
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i] != null)
                    ids.Add(orders[i].Id);
            }

            for (int i = 0; i < ids.Count; i++)
            {
                workApi.TryCancel(ids[i]);
            }
        }
    }
}
