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
        [Tooltip("AlwaysMainCamera: uses Camera.main for every input calculation.\nUseFacadeReference: uses the specific camera assigned in the BuildeableTilemap component.")]
        public TmbsCameraMode cameraMode = TmbsCameraMode.AlwaysMainCamera;

        [Header("Preview")]
        [Tooltip("Determines when the build preview is visible (Always, Never, or ModeBased).")]
        public PreviewPolicy previewPolicy = PreviewPolicy.AlwaysOn;
        
        [Tooltip("The default tile asset used for building if no selection is provided.")]
        public TileBase buildTile;
        
        [Tooltip("Tile shown in the preview when the placement is valid.")]
        public TileBase previewValidTile;
        
        [Tooltip("Tile shown in the preview when the placement is blocked or invalid.")]
        public TileBase previewInvalidTile;

        [Header("History")]
        [Tooltip("Configures Undo/Redo capabilities and history retention.")]
        public TmbsHistoryConfig history = new TmbsHistoryConfig();

        [SerializeField, HideInInspector]
        private int historyCapacity = -1;

        [Header("Metadata")]
        [Tooltip("Maximum number of build records the system can store in memory.")]
        [Min(0)]
        public int metadataInitialCapacity = 4096;

        [Header("Bounds")]
        [Tooltip("If enabled, builds will be clamped to the boundaries defined by Bounds Validators.")]
        public bool clampDragBoundsToBoundsValidator = false;

        [Header("Pending Construction")]
        [Tooltip("Configuration for deferred/pending build mode. Only active when ExecutionMode is Pending.")]
        public TmbsPendingConstructionConfig pendingConstruction = new TmbsPendingConstructionConfig();

        [Header("Validation")]
        [Tooltip("List of global validators that all build operations must pass.")]
        public List<ValidatorEntry> validators = new List<ValidatorEntry>();

        public TmbsPendingConstructionConfig GetRuntimePendingConfig()
        {
            return pendingConstruction ?? new TmbsPendingConstructionConfig();
        }

        public TmbsHistoryConfig GetRuntimeHistoryConfig()
        {
            var source = history ?? new TmbsHistoryConfig();

            return new TmbsHistoryConfig
            {
                enableUndoRedo = source.enableUndoRedo,
                capacity = Mathf.Max(0, source.capacity),
                clearOnDisable = source.clearOnDisable,
                emitRegionModifiedEvents = source.emitRegionModifiedEvents
            };
        }

        public int GetRuntimeMetadataInitialCapacity()
        {
            return Mathf.Max(0, metadataInitialCapacity);
        }

        public bool GetRuntimeClampDragBoundsToBoundsValidator()
        {
            return clampDragBoundsToBoundsValidator;
        }

        public TmbsCameraMode GetRuntimeCameraMode()
        {
            return cameraMode;
        }

        private void OnValidate()
        {
            if (input == null)
                input = new TmbsInputConfig();

            if (history == null)
                history = new TmbsHistoryConfig();
            // Migrate legacy historyCapacity if present. This ensures old assets are migrated in the editor
            // but prevents runtime getters from mutating assets.
            if (historyCapacity > 0)
            {
                if (history.capacity <= 0)
                    history.capacity = historyCapacity;

                historyCapacity = -1;
            }

            history.capacity = Mathf.Max(0, history.capacity);
            metadataInitialCapacity = Mathf.Max(0, metadataInitialCapacity);
        }
    }
}