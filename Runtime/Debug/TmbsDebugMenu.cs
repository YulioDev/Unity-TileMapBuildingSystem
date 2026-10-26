using System;
using System.Collections.Generic;
using TMBS.Core.Pending;
using TMBS.Runtime.Config;
using TMBS.Runtime.Interface;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TMBS.Runtime.Debugging
{
    /// <summary>
    /// Interactive IMGUI-based debugging overlay for TMBS.
    /// Provides real-time metrics, live execution mode toggling, manual progress stepping,
    /// auto-ticker worker simulation, and detailed pending order inspection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TmbsDebugMenu : MonoBehaviour
    {
        [Header("Target TMBS Reference")]
        [Tooltip("The BuildeableTilemap instance to monitor and control. Auto-discovered if null.")]
        [SerializeField] private BuildeableTilemap buildeableTilemap;

        [Header("Runtime State")]
        [SerializeField] private bool isOpen = false;

        private const int WindowId = 884422;
        private Rect _windowRect = new Rect(24, 24, 520, 680);
        private Vector2 _scrollPos;
        private bool _isStylesInitialized;

        // Auto-ticker state
        private bool _autoTickerEnabled = false;
        private float _tickerTimer = 0f;
        private float _tickerInterval = 0.2f;
        private int _manualWorkAmount = 1;
        private int _manualResourceAmount = 1;
        private bool _tickerDeliverResources = true;
        private bool _pauseSimulation = false;

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
            ResolveBuildeableTilemap();
            SyncWithConfig();
        }

        private void OnEnable()
        {
            ResolveBuildeableTilemap();
            SyncWithConfig();
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
            _tickerTimer += Time.unscaledDeltaTime;
            if (_tickerTimer < _tickerInterval)
                return;

            _tickerTimer = 0f;

            if (buildeableTilemap == null) return;
            var workApi = buildeableTilemap.PendingWorkApi;
            if (workApi == null) return;

            var all = workApi.GetAll();
            if (all == null || all.Count == 0) return;

            var policy = GetResourcePolicy();

            for (int o = 0; o < all.Count; o++)
            {
                var order = all[o];
                if (order == null || order.Cells == null) continue;

                var cells = order.Cells;
                for (int c = 0; c < cells.Length; c++)
                {
                    var cell = cells[c];
                    if (cell.IsCompleted) continue;

                    int allowedWork = cell.WorkAllowed(policy);

                    if (_tickerDeliverResources && cell.ResourceDelivered < cell.ResourceRequired && cell.WorkDone >= allowedWork)
                    {
                        int needed = cell.ResourceRequired - cell.ResourceDelivered;
                        int deliver = Mathf.Min(needed, _manualResourceAmount);
                        if (deliver > 0)
                        {
                            workApi.TryDeliverResources(cell.Cell, deliver);
                            return;
                        }
                    }

                    if (cell.WorkDone < allowedWork)
                    {
                        int remainingWork = allowedWork - cell.WorkDone;
                        int workToAdd = Mathf.Min(remainingWork, _manualWorkAmount);
                        workApi.TryAddWork(cell.Cell, workToAdd);

                        if (workApi.TryGetCellStatus(cell.Cell, out var updated) && updated.IsWorkComplete)
                        {
                            workApi.TryCompleteCell(cell.Cell);
                        }
                        return;
                    }

                    if (cell.IsWorkComplete && !cell.IsCompleted)
                    {
                        if (workApi.TryCompleteCell(cell.Cell))
                            return;
                    }
                }
            }
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
            float maxW = Mathf.Min(520f, Mathf.Max(320f, Screen.width - 24f));
            float maxH = Mathf.Min(680f, Mathf.Max(240f, Screen.height - 24f));
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

            _isStylesInitialized = true;
        }

        private void DrawWindowContent(int windowId)
        {
            // Title Bar Controls
            GUILayout.BeginHorizontal();
            GUILayout.Label("Herramientas de Construcción y Pendientes", _headerStyle);
            GUILayout.FlexibleSpace();

            bool prevPause = _pauseSimulation;
            _pauseSimulation = GUILayout.Toggle(_pauseSimulation, "Pausa", GUILayout.Width(60));
            if (_pauseSimulation != prevPause)
            {
                Time.timeScale = _pauseSimulation ? 0f : 1f;
            }

            if (GUILayout.Button("✕", GUILayout.Width(26), GUILayout.Height(20)))
            {
                Close();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            DrawModeSection();

            GUILayout.Space(6);

            DrawMetricsSection();

            GUILayout.Space(6);

            DrawQuickActionsSection();

            GUILayout.Space(6);

            DrawAutoTickerSection();

            GUILayout.Space(6);

            DrawOrdersInspectorSection();

            // Allow dragging from anywhere on header label area without blocking top-right controls
            float dragWidth = Mathf.Max(60f, _windowRect.width - 110f);
            GUI.DragWindow(new Rect(0, 0, dragWidth, 30));
        }

        private void DrawModeSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            var currentMode = buildeableTilemap != null && buildeableTilemap.RootConfig != null
                ? buildeableTilemap.RootConfig.executionMode
                : ExecutionMode.Immediate;

            GUILayout.Label("Modo de Ejecución:", _subHeaderStyle, GUILayout.Width(130));

            string modeText = currentMode == ExecutionMode.Immediate
                ? "<color=#55ff88>INMEDIATO (Directo al Tilemap)</color>"
                : "<color=#55ccff>PENDIENTE (Órdenes Diferidas)</color>";

            var richLabel = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold };
            GUILayout.Label(modeText, richLabel);

            GUILayout.FlexibleSpace();

            string buttonText = currentMode == ExecutionMode.Immediate
                ? "Cambiar a PENDING"
                : "Cambiar a IMMEDIATE";

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
            GUILayout.Label("Métricas Globales", _subHeaderStyle);

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
            DrawMetricItem("Órdenes:", orderCount.ToString());
            DrawMetricItem("Celdas:", totalCells.ToString());
            DrawMetricItem("Completadas:", completedCells.ToString());
            DrawMetricItem("En Espera:", waitingCells.ToString());
            DrawMetricItem("Listas:", readyCells.ToString());
            DrawMetricItem("En Curso:", inProgressCells.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawMetricItem("Progreso Trabajo:", $"{totalWorkDone}/{totalWorkReq}");
            DrawMetricItem("Recursos Entregados:", $"{totalResDelivered}/{totalResReq}");
            float overallProgress = totalWorkReq > 0 ? (float)totalWorkDone / totalWorkReq : 0f;
            DrawMetricItem("Avance Global:", $"{Mathf.RoundToInt(overallProgress * 100f)}%");
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
            GUILayout.Label("Acciones Rápidas", _subHeaderStyle);

            var workApi = buildeableTilemap != null ? buildeableTilemap.PendingWorkApi : null;
            bool hasOrders = workApi != null && workApi.GetAll() != null && workApi.GetAll().Count > 0;

            GUI.enabled = hasOrders;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"+1 Trabajo", GUILayout.Height(24)))
            {
                AdvanceAllWork(1);
            }
            if (GUILayout.Button($"+{_manualWorkAmount} Trabajo", GUILayout.Height(24)))
            {
                AdvanceAllWork(_manualWorkAmount);
            }
            if (GUILayout.Button("Entregar Recursos", GUILayout.Height(24)))
            {
                DeliverAllNeededResources();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Completar Siguiente Celda", GUILayout.Height(24)))
            {
                CompleteNextCell();
            }
            var origColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.35f);
            if (GUILayout.Button("Completar Todo", GUILayout.Height(24)))
            {
                CompleteAllPending();
            }
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
            if (GUILayout.Button("Cancelar Todas", GUILayout.Height(24)))
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
            GUILayout.BeginHorizontal();
            _autoTickerEnabled = GUILayout.Toggle(_autoTickerEnabled, "Auto-Avance Continuo (Simular Trabajadores)", GUILayout.Width(270));
            _tickerDeliverResources = GUILayout.Toggle(_tickerDeliverResources, "Auto-Entregar Recursos");
            GUILayout.EndHorizontal();

            if (_autoTickerEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Cadencia: {_tickerInterval:F2}s", GUILayout.Width(110));
                _tickerInterval = GUILayout.HorizontalSlider(_tickerInterval, 0.05f, 1.0f);
                GUILayout.Space(12);
                GUILayout.Label($"Trabajo/Tick: {_manualWorkAmount}", GUILayout.Width(110));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawOrdersInspectorSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Inspector de Órdenes Pendientes", _subHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var workApi = buildeableTilemap != null ? buildeableTilemap.PendingWorkApi : null;
            var currentMode = buildeableTilemap != null && buildeableTilemap.RootConfig != null
                ? buildeableTilemap.RootConfig.executionMode
                : ExecutionMode.Immediate;

            if (currentMode == ExecutionMode.Immediate)
            {
                GUILayout.Space(12);
                GUILayout.Label("El sistema se encuentra en modo INMEDIATO.\nLas piezas se colocan directamente en el tilemap sin crear órdenes pendientes.\nPara probar la cola de trabajo diferida, pulsa 'Cambiar a PENDING' en la barra superior.", _metricLabelStyle);
                GUILayout.Space(12);
                GUILayout.EndVertical();
                return;
            }

            var orders = workApi != null ? workApi.GetAll() : null;

            if (orders == null || orders.Count == 0)
            {
                GUILayout.Space(16);
                GUILayout.Label("No hay órdenes pendientes activas en la cola.\nSelecciona un bloque de la paleta y arrastra sobre el mapa para crear nuevas órdenes.", _metricLabelStyle);
                GUILayout.Space(16);
                GUILayout.EndVertical();
                return;
            }

            float scrollHeight = Mathf.Max(120f, _windowRect.height - 440f);
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
            GUILayout.Label($"Orden #{order.Id}", EditorHeaderStyle(), GUILayout.Width(75));

            string stateName = order.State.ToString();
            GUIStyle badgeStyle = GetBadgeStyle(order.State);
            GUILayout.Box(stateName, badgeStyle, GUILayout.Width(130), GUILayout.Height(18));

            // Buildable definition name
            string buildableName = order.PrimaryBuildableData != null
                ? order.PrimaryBuildableData.displayName
                : (order.Cells != null && order.Cells.Length > 0 && order.Cells[0].Tile != null ? order.Cells[0].Tile.name : "Tile");
            GUILayout.Label($"[{buildableName}]", _metricValueStyle);

            GUILayout.FlexibleSpace();

            var origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("✕ Cancelar", GUILayout.Width(75), GUILayout.Height(18)))
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
            GUILayout.Label($"Trabajo: {cell.WorkDone}/{cell.WorkRequired}", _metricLabelStyle, GUILayout.Width(100));

            if (cell.ResourceRequired > 0)
            {
                GUILayout.Label($"Recursos: {cell.ResourceDelivered}/{cell.ResourceRequired}", _metricLabelStyle, GUILayout.Width(110));
            }
            else
            {
                GUILayout.Label("Sin recursos", _metricLabelStyle, GUILayout.Width(110));
            }

            GUILayout.FlexibleSpace();

            if (cell.IsCompleted)
            {
                var greenBox = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.2f, 1f, 0.4f) }
                };
                GUILayout.Box("Completado", greenBox, GUILayout.Width(80), GUILayout.Height(18));
            }
            else
            {
                var policy = GetResourcePolicy();
                if (cell.ResourceDelivered < cell.ResourceRequired)
                {
                    if (GUILayout.Button("+ Recurso", GUILayout.Width(70), GUILayout.Height(18)))
                    {
                        int needed = cell.ResourceRequired - cell.ResourceDelivered;
                        workApi.TryDeliverResources(cell.Cell, Mathf.Min(needed, _manualResourceAmount));
                    }
                }
                if (cell.WorkDone < cell.WorkAllowed(policy))
                {
                    if (GUILayout.Button("+ Trabajo", GUILayout.Width(70), GUILayout.Height(18)))
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

                if (GUILayout.Button("Completar", GUILayout.Width(70), GUILayout.Height(18)))
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

        private GUIStyle EditorHeaderStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
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

        #region Operations

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

        #endregion
    }
}
