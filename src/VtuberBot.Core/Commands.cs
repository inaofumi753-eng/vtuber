namespace VtuberBot.Core;

public sealed class CommandRequest
{
    public CommandRequest(string name, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command name must not be empty.", nameof(name));

        ArgumentNullException.ThrowIfNull(arguments);

        Name = name.Trim();
        Arguments = arguments.ToArray();
    }

    public string Name { get; }
    public IReadOnlyList<string> Arguments { get; }

    public static CommandRequest Parse(string input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        var parts = input.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            throw new ArgumentException("Command input must not be empty.", nameof(input));

        return new(parts[0], parts.Skip(1).ToArray());
    }
}

public sealed record CommandResult(bool Success, string Message)
{
    public static CommandResult Succeeded(string message) =>
        new(true, message);

    public static CommandResult Failed(string message) =>
        new(false, message);
}

public sealed class CommandDefinition
{
    public CommandDefinition(
        string name,
        string description,
        Func<CommandRequest, CommandResult> handler)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command name must not be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Command description must not be empty.", nameof(description));

        ArgumentNullException.ThrowIfNull(handler);

        Name = name.Trim();
        Description = description.Trim();
        Handler = handler;
    }

    public string Name { get; }
    public string Description { get; }
    public Func<CommandRequest, CommandResult> Handler { get; }
}

public sealed class CommandRouter
{
    private readonly object gate = new();
    private readonly Dictionary<string, CommandDefinition> commands =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger logger;

    public CommandRouter(ILogger logger) => this.logger = logger;

    public IReadOnlyList<CommandDefinition> Definitions
    {
        get
        {
            lock (gate)
            {
                return commands.Values
                    .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }
    }

    public void Register(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);

        lock (gate)
        {
            if (!commands.TryAdd(command.Name, command))
                throw new InvalidOperationException(
                    $"Command '{command.Name}' is already registered.");
        }
    }

    public CommandResult Execute(string input)
    {
        try
        {
            return Execute(CommandRequest.Parse(input));
        }
        catch (ArgumentException exception)
        {
            return CommandResult.Failed(exception.Message);
        }
    }

    public CommandResult Execute(CommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        CommandDefinition? command;
        lock (gate)
            commands.TryGetValue(request.Name, out command);

        if (command is null)
            return CommandResult.Failed($"Unknown command: {request.Name}");

        try
        {
            return command.Handler(request);
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Command '{0}' failed.", command.Name);
            return CommandResult.Failed($"Command failed: {exception.Message}");
        }
    }

    public string GetHelpText()
    {
        var definitions = Definitions;
        if (definitions.Count == 0)
            return "No commands registered.";

        return string.Join(
            Environment.NewLine,
            definitions.Select(command => $"{command.Name} — {command.Description}"));
    }
}
