using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.Events
{
    // Use small structs so raising does not allocate.
    public interface IEvent { }

    // Synchronous. Handlers may subscribe, unsubscribe or raise again during a raise.
    public static class EventBus<T> where T : struct, IEvent
    {
        private static readonly List<Action<T>> Handlers = new();
        // One reused handler snapshot per nesting depth, so a nested raise does not disturb the outer one.
        private static readonly List<List<Action<T>>> Snapshots = new();
        private static int _depth;

        static EventBus() => EventBusReset.Register(() => Handlers.Clear());

        public static void Subscribe(Action<T> handler)
        {
            if (handler != null && !Handlers.Contains(handler)) Handlers.Add(handler);
        }

        public static void Unsubscribe(Action<T> handler) => Handlers.Remove(handler);

        public static void Raise(T evt)
        {
            if (_depth == Snapshots.Count) Snapshots.Add(new List<Action<T>>());
            var snapshot = Snapshots[_depth++];
            snapshot.AddRange(Handlers);
            try
            {
                foreach (var handler in snapshot)
                {
                    try
                    {
                        handler(evt);
                    }
                    catch (Exception e)
                    {
                        // One faulty listener must not stop the others.
                        Debug.LogException(e);
                    }
                }
            }
            finally
            {
                snapshot.Clear();
                _depth--;
            }
        }
    }

    // Needed when Editor domain reload is disabled.
    internal static class EventBusReset
    {
        private static readonly List<Action> Clearers = new();

        public static void Register(Action clear) => Clearers.Add(clear);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            foreach (var clear in Clearers) clear();
        }
    }
}
