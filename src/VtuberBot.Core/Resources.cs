namespace VtuberBot.Core;

public sealed class ResourceManager
{
    private readonly ILogger logger;
    private readonly Dictionary<string, Action> resources = [];
    private bool cleaned;

    public ResourceManager(ILogger logger) => this.logger = logger;

    public void Register(string name, Action cleanup)
    {
        if (cleaned)
            throw new InvalidOperationException("Cannot register resources after cleanup.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Resource name must be a non-empty string.", nameof(name));

        ArgumentNullException.ThrowIfNull(cleanup);
        resources[name] = cleanup;
    }

    public void CleanupAll()
    {
        if (cleaned)
            return;

        cleaned = true;

        foreach (var resource in resources.Reverse())
        {
            try
            {
                resource.Value();
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Cleanup failed for resource {0}", resource.Key);
            }
        }

        resources.Clear();
    }
}
