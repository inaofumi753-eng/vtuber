using Tomlyn;
using Tomlyn.Model;

namespace VtuberBot.Core;

public sealed class ConfigException : Exception
{
    public ConfigException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed record AppConfig(
    string Name,
    string DatabasePath,
    string BaseDirectory,
    LogLevel LogLevel,
    string? ConfigPath)
{
    public string LogDirectory => Path.Combine(BaseDirectory, "logs");

    public static AppConfig Load(string? configPath = null)
    {
        var path = configPath is null
            ? Path.Combine(Directory.GetCurrentDirectory(), "config.toml")
            : Path.GetFullPath(configPath);

        TomlTable root;
        try
        {
            root = File.Exists(path)
                ? TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path)) ?? new TomlTable()
                : new TomlTable();
        }
        catch (Exception exception)
        {
            throw new ConfigException($"Invalid TOML in {path}: {exception.Message}", exception);
        }

        TomlTable Table(string name) =>
            root.TryGetValue(name, out var value) && value is TomlTable table
                ? table
                : new TomlTable();

        string StringValue(TomlTable table, string key, string defaultValue) =>
            !table.TryGetValue(key, out var value)
                ? defaultValue
                : value as string ?? throw new ConfigException($"[{key}] must be a string.");

        var baseDirectory = configPath is null
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(path)!;

        var app = Table("app");
        var database = Table("database");
        var logging = Table("logging");

        var name = StringValue(app, "name", "VTuber Bot");
        var databasePath = StringValue(database, "path", Path.Combine("data", "vtuber.sqlite3"));
        var level = StringValue(logging, "level", "INFO");

        if (string.IsNullOrWhiteSpace(name))
            throw new ConfigException("[app].name must not be empty.");

        if (!Enum.TryParse(level, ignoreCase: true, out LogLevel logLevel))
            throw new ConfigException($"Invalid logging level: {level}.");

        return new(
            name.Trim(),
            Path.GetFullPath(databasePath, baseDirectory),
            baseDirectory,
            logLevel,
            File.Exists(path) ? path : null);
    }
}
