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
        public readonly bool HasPipette;
        public readonly bool HasAlternateModifier;

        public InputCapabilities(
            bool hasPoint,
            bool hasConfirm,
            bool hasCancel,
            bool hasDrag,
            bool hasUndo,
            bool hasRedo,
            bool hasPipette,
            bool hasAlternateModifier)
        {
            HasPoint = hasPoint;
            HasConfirm = hasConfirm;
            HasCancel = hasCancel;
            HasDrag = hasDrag;
            HasUndo = hasUndo;
            HasRedo = hasRedo;
            HasPipette = hasPipette;
            HasAlternateModifier = hasAlternateModifier;
        }
    }
}

