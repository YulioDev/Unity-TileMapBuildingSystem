using System.Collections.Generic;
using TMBS.Runtime.Config;
using TMBS.Core.Preview;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TMBS.Runtime.Config
{
    [CreateAssetMenu(menuName = "TMBS/TmbsRootConfig")]
    public sealed class TmbsRootConfig : ScriptableObject
    {
        [Header("Execution")]
        [Tooltip("Defines how building actions are executed. Immediate: places tiles instantly. Pending: creates deferred build orders for external agents.")]
        public ExecutionMode executionMode = ExecutionMode.Immediate;

        [Header("Input")]
        [Tooltip("Configuration for input handling and adapters.")]
        public TmbsInputConfig input = new TmbsInputConfig();

        [Header("Camera")]
        [Tooltip("AlwaysMainCamera: uses Camera.main for every input calculation.\nUseInterfaceReference: uses the specific camera assigned in the BuildeableTilemap component.")]
        public TmbsCameraMode cameraMode = TmbsCameraMode.AlwaysMainCamera;

        [Header("Preview")]
        [Tooltip("Determines when the build preview is visible (Always, Never, or ModeBased).")]
        public PreviewPolicy previewPolicy = PreviewPolicy.AlwaysOn;

        [Tooltip("Colors used for the preview overlay. The build tile is tinted with these colors to indicate validity.")]
        public TmbsPreviewConfig previewOverlay = new TmbsPreviewConfig();

        [Header("History")]
        [Tooltip("Configures Undo/Redo capabilities and history retention.")]
        public TmbsHistoryConfig history = new TmbsHistoryConfig();

        [SerializeField, HideInInspector]
        private int historyCapacity = -1;

        [Header("Metadata")]
        [Tooltip("Maximum number of build records the system can store in memory.")]
        [Min(0)]
        public int recordCapacity = 4096;

        [Header("Bounds")]
        [Tooltip("If enabled, builds will be clamped to the boundaries defined by Bounds Validators.")]
        public bool clampToBounds = false;

        [Header("Pending Construction")]
        [Tooltip("Configuration for deferred/pending build mode. Only active when ExecutionMode is Pending.")]
        public TmbsPendingConfig pending = new TmbsPendingConfig();

        [Header("Pending Debug")]
        [Tooltip("Debug-only options for slowly completing pending constructions without external agents.")]
        public TmbsPendingDebugConfig pendingDebug = new TmbsPendingDebugConfig();

        [Header("Debug Menu")]
        [Tooltip("Configuration for the in-game debug overlay menu.")]
        public TmbsDebugMenuConfig debugMenu = new TmbsDebugMenuConfig();

        [Header("Default Buildable (Optional)")]
        [Tooltip("Optional initial buildable data asset.")]
        public TmbsBuildableData defaultBuildable;

        [Header("Validation")]
        [Tooltip("List of global validators that all build operations must pass.")]
        public List<ValidatorEntry> validators = new List<ValidatorEntry>();

        public TmbsDebugMenuConfig GetDebugMenuConfig()
        {
            return debugMenu ?? new TmbsDebugMenuConfig();
        }

        public bool IsDebugMenuEnabled()
        {
            return debugMenu != null && debugMenu.enabled;
        }

        public TmbsPendingConfig GetPendingConfig()
        {
            return pending ?? new TmbsPendingConfig();
        }

        public TmbsPendingDebugConfig GetPendingDebugConfig()
        {
            return pendingDebug ?? new TmbsPendingDebugConfig();
        }

        public TmbsHistoryConfig GetHistoryConfig()
        {
            var source = history ?? new TmbsHistoryConfig();

            return new TmbsHistoryConfig
            {
                enableUndoRedo = source.enableUndoRedo,
                capacity = Mathf.Max(0, source.capacity),
                clearOnDisable = source.clearOnDisable,
                emitRegionEvents = source.emitRegionEvents
            };
        }

        public int GetRecordCapacity()
        {
            return Mathf.Max(0, recordCapacity);
        }

        public bool ShouldClampToBounds()
        {
            return clampToBounds;
        }

        public TmbsCameraMode GetCameraMode()
        {
            return cameraMode;
        }

        private void OnValidate()
        {
            if (input == null)
                input = new TmbsInputConfig();

            if (debugMenu == null)
                debugMenu = new TmbsDebugMenuConfig();

            if (history == null)
                history = new TmbsHistoryConfig();
            if (historyCapacity > 0)
            {
                if (history.capacity <= 0)
                    history.capacity = historyCapacity;

                historyCapacity = -1;
            }

            history.capacity = Mathf.Max(0, history.capacity);
            recordCapacity = Mathf.Max(0, recordCapacity);
        }
    }
}
