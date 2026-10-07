namespace VtuberBot.Core;

public sealed record AppEvent
{
    public AppEvent(string name, object? payload = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Event name must be a non-empty string.", nameof(name));

        Name = name;
        Payload = payload;
    }

    public string Name { get; }
    public object? Payload { get; }
}

public sealed class EventManager
{
    private readonly object gate = new();
    private readonly Dictionary<string, List<Action<AppEvent>>> handlers = [];
    private readonly ILogger logger;

    public EventManager(ILogger logger) => this.logger = logger;

    public void Subscribe(string eventName, Action<AppEvent> handler)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name must be a non-empty string.", nameof(eventName));

        ArgumentNullException.ThrowIfNull(handler);

        lock (gate)
        {
            if (!handlers.TryGetValue(eventName, out var list))
                handlers[eventName] = list = [];

            if (!list.Contains(handler))
                list.Add(handler);
        }
    }

    public void Unsubscribe(string eventName, Action<AppEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (gate)
        {
            if (!handlers.TryGetValue(eventName, out var list))
                return;

            list.Remove(handler);
            if (list.Count == 0)
                handlers.Remove(eventName);
        }
    }

    public void Emit(AppEvent appEvent)
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        Action<AppEvent>[] snapshot;
        lock (gate)
            snapshot = handlers.TryGetValue(appEvent.Name, out var list) ? list.ToArray() : [];

        foreach (var handler in snapshot)
        {
            try
            {
                handler(appEvent);
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Event handler failed for {0}", appEvent.Name);
            }
        }
    }
}
