using System;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public sealed class TmbsHistoryConfig
    {
        [Tooltip("If enabled, build operations can be undone and redone.")]
        public bool enableUndoRedo = true;

        [Tooltip("Maximum number of commands to store in the history.")]
        [Min(0)]
        public int capacity = 256;

        [Tooltip("If enabled, the undo/redo history will be cleared when the BuildeableTilemap is disabled.")]
        public bool clearOnDisable = false;

        [Tooltip("If enabled, the system will emit events identifying which regions of the tilemap were modified.")]
        public bool emitRegionModifiedEvents = true;
    }
}
