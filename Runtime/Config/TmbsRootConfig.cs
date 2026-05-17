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
        public ExecutionMode executionMode = ExecutionMode.ImmediateOnly;
        public PreviewPolicy previewPolicy = PreviewPolicy.AlwaysOn;

        public TileBase buildTile;
        public TileBase previewValidTile;
        public TileBase previewInvalidTile;

        public int historyCapacity = 256;
        public int metadataCapacity = 4096;

        public List<ValidatorEntry> validators = new List<ValidatorEntry>();
    }
}

