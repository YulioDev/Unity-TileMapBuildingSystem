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
        public ExecutionMode executionMode = ExecutionMode.ImmediateOnly;

        [Header("Input")]
        public TmbsInputConfig input = new TmbsInputConfig();

        [Header("Preview")]
        public PreviewPolicy previewPolicy = PreviewPolicy.AlwaysOn;
        public TileBase buildTile;
        public TileBase previewValidTile;
        public TileBase previewInvalidTile;

        [Header("History")]
        public TmbsHistoryConfig history = new TmbsHistoryConfig();

        [SerializeField, HideInInspector]
        private int historyCapacity = -1;

        [Header("Metadata")]
        [Min(0)]
        public int metadataCapacity = 4096;

        [Header("Performance")]
        [Range(0f, 1f)]
        public float sparseWriteDenseThreshold = 0.65f;

        [Header("Validation")]
        public List<ValidatorEntry> validators = new List<ValidatorEntry>();

        private void OnValidate()
        {
            if (input == null)
                input = new TmbsInputConfig();

            if (history == null)
                history = new TmbsHistoryConfig();

            if (historyCapacity > 0 && history != null && history.capacity <= 0)
            {
                history.capacity = historyCapacity;
                historyCapacity = -1;
            }

            history.capacity = Mathf.Max(0, history.capacity);
            metadataCapacity = Mathf.Max(0, metadataCapacity);
            sparseWriteDenseThreshold = Mathf.Clamp01(sparseWriteDenseThreshold);
        }
    }
}

