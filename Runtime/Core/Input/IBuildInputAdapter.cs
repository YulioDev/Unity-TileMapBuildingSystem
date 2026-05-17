using System;
using TMBS.Core.Intents;

namespace TMBS.Core.Input
{
    public interface IBuildInputAdapter
    {
        InputCapabilities Capabilities { get; }
        event Action<BuildIntent> BuildIntentRaised;
        void Enable();
        void Disable();
    }
}

