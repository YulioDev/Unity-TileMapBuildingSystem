using System;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public sealed class TmbsHistoryConfig
    {
        public bool enableUndoRedo = true;

        [Min(0)]
        public int capacity = 256;

        public bool clearOnDisable = false;
        public bool emitRegionModifiedEvents = true;
    }
}
