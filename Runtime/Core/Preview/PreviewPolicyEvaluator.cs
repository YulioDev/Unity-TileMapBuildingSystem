using TMBS.Core.Modes;

namespace TMBS.Core.Preview
{
    public sealed class PreviewPolicyEvaluator
    {
        private readonly PreviewPolicy _globalPolicy;

        public PreviewPolicyEvaluator(PreviewPolicy globalPolicy)
        {
            _globalPolicy = globalPolicy;
        }

        public bool ShouldShowPreview(IBuildMode activeMode)
        {
            if (_globalPolicy == PreviewPolicy.StrictOff)
            {
                return false;
            }

            if (_globalPolicy == PreviewPolicy.AlwaysOn)
            {
                return true;
            }

            
            
            return activeMode != null && activeMode.RequiresPreview;
        }

        public bool IsModeAllowed(IBuildMode mode)
        {
            
            if (_globalPolicy == PreviewPolicy.StrictOff && mode.RequiresPreview)
            {
                return false;
            }
            return true;
        }
    }
}

