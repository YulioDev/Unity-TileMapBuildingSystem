using TMBS.Core.Modes;

namespace TMBS.Core.Preview
{
    public sealed class PreviewToggle
    {
        private readonly PreviewPolicy _policy;

        public PreviewToggle(PreviewPolicy policy)
        {
            _policy = policy;
        }

        public bool ShouldShowPreview(IBuildMode mode)
        {
            switch (_policy)
            {
                case PreviewPolicy.AlwaysOn:
                    return true;
                case PreviewPolicy.StrictOff:
                    return false;
                case PreviewPolicy.SoftOff:
                    return mode != null && mode.RequiresPreview;
                default:
                    return true;
            }
        }
    }
}
