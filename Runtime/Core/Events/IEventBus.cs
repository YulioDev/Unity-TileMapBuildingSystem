using System;

namespace TMBS.Core.Events
{
    public interface IEventBus
    {
        void Publish<T>(in T evt) where T : struct;
        IDisposable Subscribe<T>(Action<T> handler) where T : struct;
    }
}

