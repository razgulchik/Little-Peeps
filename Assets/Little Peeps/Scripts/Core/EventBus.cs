using System;
using System.Collections.Generic;

public static class EventBus<T>
{
    private static readonly List<Action<T>> subscribers = new();

    public static void Subscribe(Action<T> handler)
    {
        if (!subscribers.Contains(handler))
            subscribers.Add(handler);
    }

    public static void Unsubscribe(Action<T> handler)
    {
        subscribers.Remove(handler);
    }

    // Copy list before iteration so mid-dispatch unsubscribes are safe
    public static void Publish(T eventData)
    {
        var snapshot = new List<Action<T>>(subscribers);
        foreach (var handler in snapshot)
            handler(eventData);
    }
}
