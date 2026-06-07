using System;
using System.Collections.Generic;

namespace NexusFramework.GAS.ECS
{
    public static class GASInternalBridge
    {
        private static readonly List<Action> _pendingEvents = new();
        private static readonly object _lock = new();

        public static event Action OnBeforeDrain;
        public static event Action<object> OnEventEnqueued;

        public static void Enqueue(Action action)
        {
            lock (_lock)
            {
                _pendingEvents.Add(action);
            }
        }

        public static void Enqueue<T>(T evt) where T : struct
        {
            lock (_lock)
            {
                _pendingEvents.Add(() => OnEventEnqueued?.Invoke(evt));
            }
        }

        public static void Drain()
        {
            Action[] snapshot;
            lock (_lock)
            {
                if (_pendingEvents.Count == 0) return;
                snapshot = _pendingEvents.ToArray();
                _pendingEvents.Clear();
            }

            OnBeforeDrain?.Invoke();
            foreach (var action in snapshot)
                action?.Invoke();
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _pendingEvents.Clear();
            }
        }
    }
}
