namespace TMBS.Core.Input
{
    public readonly struct InputCapabilities
    {
        public readonly bool HasPoint;
        public readonly bool HasConfirm;
        public readonly bool HasCancel;
        public readonly bool HasDrag;
        public readonly bool HasUndo;
        public readonly bool HasRedo;
        public readonly bool HasAlternateModifier;
        public readonly bool HasRotate;
        public readonly bool HasMirror;

        public InputCapabilities(
            bool hasPoint,
            bool hasConfirm,
            bool hasCancel,
            bool hasDrag,
            bool hasUndo,
            bool hasRedo,
            bool hasAlternateModifier,
            bool hasRotate = false,
            bool hasMirror = false)
        {
            HasPoint = hasPoint;
            HasConfirm = hasConfirm;
            HasCancel = hasCancel;
            HasDrag = hasDrag;
            HasUndo = hasUndo;
            HasRedo = hasRedo;
            HasAlternateModifier = hasAlternateModifier;
            HasRotate = hasRotate;
            HasMirror = hasMirror;
        }
    }
}

