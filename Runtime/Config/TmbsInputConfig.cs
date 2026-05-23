using System;
using UnityEngine;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public sealed class TmbsInputConfig
    {
        [Tooltip(
            "None: Input will not be processed.\n" +
            "Mouse: Uses the built-in mouse and keyboard input adapter.\n" +
            "ExternalProvided: Requires manual injection via code before enabling BuildeableTilemap.")]
        public TmbsInputMode mode = TmbsInputMode.Mouse;

        [Tooltip("If enabled, build actions will be blocked if the input adapter does not meet all required capabilities.")]
        public bool strictInputValidation = false;

        public bool requirePoint = true;
        public bool requireConfirm = true;
        public bool requireCancel = true;
        public bool requireDrag = true;
        public bool requireAlternateModifier = true;

        public bool allowUndoInput = true;
        public bool allowRedoInput = true;
    }
}