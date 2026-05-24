using System;
using System.Collections.Generic;

namespace TMBS.Core.Events
{
    public sealed class SimpleEventBus : IEventBus
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>(32);

        public void Publish<T>(in T evt) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var obj))
            {
                var list = (List<Action<T>>)obj;
                var snapshot = list.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i](evt);
                }
            }
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!_handlers.TryGetValue(typeof(T), out var obj))
            {
                obj = new List<Action<T>>(8);
                _handlers[typeof(T)] = obj;
            }

            var list = (List<Action<T>>)obj;
            list.Add(handler);
            return new Subscription<T>(list, handler);
        }

        private sealed class Subscription<T> : IDisposable where T : struct
        {
            private List<Action<T>> _list;
            private Action<T> _handler;

            public Subscription(List<Action<T>> list, Action<T> handler)
            {
                _list = list;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_list != null && _handler != null)
                {
                    _list.Remove(_handler);
                    _list = null;
                    _handler = null;
                }
            }
        }
    }
}

