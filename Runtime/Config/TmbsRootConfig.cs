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
        [Tooltip("Defines how building actions are executed. Currently supports Immediate mode only.")]
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
        public int metadataCapacity = 4096;

        [Header("Performance")]
        [Tooltip("Determines the threshold to switch between sparse (individual) and dense (bulk) tilemap writes.")]
        [Range(0f, 1f)]
        public float sparseWriteDenseThreshold = 0.65f;

        [Header("Bounds")]
        [Tooltip("If enabled, builds will be clamped to the boundaries defined by Bounds Validators.")]
        public bool clampDragBoundsToBoundsValidator = false;

        [Header("Validation")]
        [Tooltip("List of global validators that all build operations must pass.")]
        public List<ValidatorEntry> validators = new List<ValidatorEntry>();

        public TmbsHistoryConfig GetRuntimeHistoryConfig()
        {
            var source = history ?? new TmbsHistoryConfig();

            int resolvedCapacity = source.capacity;

            if (historyCapacity > 0 && source.capacity <= 0)
                resolvedCapacity = historyCapacity;

            return new TmbsHistoryConfig
            {
                enableUndoRedo = source.enableUndoRedo,
                capacity = Mathf.Max(0, resolvedCapacity),
                clearOnDisable = source.clearOnDisable,
                emitRegionModifiedEvents = source.emitRegionModifiedEvents
            };
        }

        public int GetRuntimeMetadataCapacity()
        {
            return Mathf.Max(0, metadataCapacity);
        }

        public float GetRuntimeSparseWriteDenseThreshold()
        {
            return Mathf.Clamp01(sparseWriteDenseThreshold);
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

            history.capacity = Mathf.Max(0, history.capacity);
            metadataCapacity = Mathf.Max(0, metadataCapacity);
            sparseWriteDenseThreshold = Mathf.Clamp01(sparseWriteDenseThreshold);
        }
    }
}