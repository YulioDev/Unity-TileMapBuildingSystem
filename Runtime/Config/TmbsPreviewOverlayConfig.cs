using System;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public sealed class TmbsPreviewOverlayConfig
    {
        [Tooltip("Color overlay applied to the tile when placement is valid.")]
        public Color validColor = new Color(0.25f, 1f, 0.25f, 0.5f);

        [Tooltip("Color overlay applied to the tile when placement is blocked or invalid.")]
        public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    }
}
