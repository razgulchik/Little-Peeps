using System;
using System.Collections.Generic;

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
