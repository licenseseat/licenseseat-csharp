using System;
using System.Collections.Generic;

namespace LicenseSeat;

/// <summary>
/// Event bus for LicenseSeat SDK events.
/// Provides a simple pub/sub mechanism for SDK events.
/// </summary>
public sealed class EventBus
{
    private readonly object _lock = new object();
    private readonly Dictionary<string, List<Subscription>> _subscriptions = new Dictionary<string, List<Subscription>>();
    private long _nextId;

    /// <summary>
    /// Subscribes to an event.
    /// </summary>
    /// <param name="eventName">The name of the event to subscribe to.</param>
    /// <param name="handler">The event handler.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    public IDisposable On(string eventName, Action<object?> handler)
    {
        if (eventName == null)
        {
            throw new ArgumentNullException(nameof(eventName));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventName, out var handlers))
            {
                handlers = new List<Subscription>();
                _subscriptions[eventName] = handlers;
            }

            var subscription = new Subscription(_nextId++, handler, () => Unsubscribe(eventName, handler));
            handlers.Add(subscription);
            return subscription;
        }
    }

    /// <summary>
    /// Subscribes to an event with a typed handler.
    /// </summary>
    /// <typeparam name="T">The expected type of the event data.</typeparam>
    /// <param name="eventName">The name of the event to subscribe to.</param>
    /// <param name="handler">The event handler.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    public IDisposable On<T>(string eventName, Action<T?> handler)
    {
        return On(eventName, data =>
        {
            if (data is T typedData)
            {
                handler(typedData);
            }
            else if (data == null)
            {
                handler(default);
            }
        });
    }

    /// <summary>
    /// Unsubscribes a handler from an event.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="handler">The handler to remove.</param>
    public void Off(string eventName, Action<object?> handler)
    {
        Unsubscribe(eventName, handler);
    }

    /// <summary>
    /// Emits an event with optional data.
    /// </summary>
    /// <param name="eventName">The name of the event to emit.</param>
    /// <param name="data">Optional event data.</param>
    public void Emit(string eventName, object? data = null)
    {
        if (eventName == null)
        {
            throw new ArgumentNullException(nameof(eventName));
        }

        List<Subscription>? handlers;
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventName, out handlers) || handlers.Count == 0)
            {
                return;
            }

            // Create a copy to avoid modification during enumeration
            handlers = new List<Subscription>(handlers);
        }

        foreach (var subscription in handlers)
        {
            try
            {
                subscription.Handler(data);
            }
            catch (Exception ex)
            {
                // Log but don't propagate errors from event handlers
                System.Diagnostics.Debug.WriteLine($"[LicenseSeat SDK] Error in event handler for {eventName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Removes all subscriptions for all events.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _subscriptions.Clear();
        }
    }

    /// <summary>
    /// Removes all subscriptions for a specific event.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    public void Clear(string eventName)
    {
        lock (_lock)
        {
            _subscriptions.Remove(eventName);
        }
    }

    /// <summary>
    /// Gets the number of subscribers for an event.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <returns>The number of subscribers.</returns>
    public int GetSubscriberCount(string eventName)
    {
        lock (_lock)
        {
            return _subscriptions.TryGetValue(eventName, out var handlers) ? handlers.Count : 0;
        }
    }

    private void Unsubscribe(string eventName, Action<object?> handler)
    {
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(eventName, out var handlers))
            {
                handlers.RemoveAll(s => s.Handler == handler);
                if (handlers.Count == 0)
                {
                    _subscriptions.Remove(eventName);
                }
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public long Id { get; }
        public Action<object?> Handler { get; }

        public Subscription(long id, Action<object?> handler, Action dispose)
        {
            Id = id;
            Handler = handler;
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _dispose();
        }
    }
}
