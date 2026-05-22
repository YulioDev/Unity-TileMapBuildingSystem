using System;

namespace TMBS.Runtime.Config
{
    [Serializable]
    public sealed class TmbsInputConfig
    {
        public TmbsInputMode mode = TmbsInputMode.SceneProvided;

        public bool strictInputValidation = false;

        public bool requirePoint = true;
        public bool requireConfirm = true;
        public bool requireCancel = true;
        public bool requireDrag = true;
        public bool requireAlternateModifier = true;

        public bool allowUndoInput = true;
        public bool allowRedoInput = true;

        public bool autoCreateDebugInputAdapter = true;
    }
}
