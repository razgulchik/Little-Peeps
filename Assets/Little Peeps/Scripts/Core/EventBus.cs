using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Non-generic companion to EventBus<T> — the one place that can wipe EVERY typed bus at once.
    //
    // Each closed EventBus<T> owns its own statics and there is no way to enumerate them, so every bus
    // registers its own Clear here the first time it is touched and ClearAll walks that list.
    //
    // Why it has to exist: with Domain Reload disabled (Enter Play Mode Options) statics SURVIVE leaving
    // play mode, so a second Play would start holding handlers of MonoBehaviours the first Play already
    // destroyed — the first Publish then calls straight into dead objects. ClearAll runs before the first
    // scene loads, so the buses are always empty when the new session's OnEnables start subscribing.
    public static class EventBus
    {
        private static readonly List<Action> clearActions = new();

        // Called once per event type, from EventBus<T>'s static constructor. The delegate targets a
        // static method, so this list never keeps a scene object alive.
        internal static void RegisterBus(Action clear) => clearActions.Add(clear);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearAll()
        {
            for (int i = 0; i < clearActions.Count; i++) clearActions[i]();
        }
    }

    // Typed static publish-subscribe bus. Subscribers change rarely (OnEnable/OnDisable) but
    // Publish runs hot (potentially thousands/sec), so we never allocate per Publish: a cached
    // array of subscribers is rebuilt only when the set actually changes (dirty flag).
    //
    // Iterating the cached array also preserves the old "snapshot" safety — a handler that
    // subscribes/unsubscribes mid-dispatch only touches `subscribers`/`dirty`, not the array we
    // are iterating; the change takes effect on the next Publish. Capturing `cache` into a local
    // keeps it safe even if a handler triggers a re-entrant Publish (which may rebuild `cache`).
    //
    // Not thread-safe by design — Unity game logic runs on the main thread.
    public static class EventBus<T>
    {
        private static readonly List<Action<T>> subscribers = new();
        private static Action<T>[] cache = Array.Empty<Action<T>>();
        private static bool dirty = false;

        // Hands this closed type to the non-generic EventBus so ClearAll can reach it. Runs on first
        // use of the bus (first Subscribe/Publish), which is necessarily before it holds a subscriber.
        static EventBus() => EventBus.RegisterBus(Clear);

        public static void Subscribe(Action<T> handler)
        {
            if (subscribers.Contains(handler)) return;
            subscribers.Add(handler);
            dirty = true;                       // set changed → cached array is stale
        }

        public static void Unsubscribe(Action<T> handler)
        {
            if (subscribers.Remove(handler))
                dirty = true;
        }

        // Drop every subscriber. Needed because the subscriber set is static: with Domain Reload
        // disabled it would otherwise survive a play-mode restart and keep handlers of destroyed
        // objects alive. Tests use it to isolate one case from the next.
        public static void Clear()
        {
            subscribers.Clear();
            dirty = true;
        }

        public static void Publish(T eventData)
        {
            if (dirty)
            {
                cache = subscribers.ToArray();  // rebuilt only after a Subscribe/Unsubscribe
                dirty = false;
            }

            var local = cache;                  // capture: a handler may re-entrantly Publish
            for (int i = 0; i < local.Length; i++)
                local[i](eventData);
        }
    }
}
