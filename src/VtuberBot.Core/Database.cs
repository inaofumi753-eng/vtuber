using System.Globalization;
using Microsoft.Data.Sqlite;

namespace VtuberBot.Core;

public sealed class SqliteSchemaException : Exception
{
    public SqliteSchemaException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class SqliteDatabase : IDisposable
{
    public const int SchemaVersion = 1;
    private const int DefaultTimeoutSeconds = 5;

    private readonly object lifecycleGate = new();
    private readonly SemaphoreSlim writerGate = new(1, 1);
    private readonly string connectionString;
    private bool open;
    private bool disposed;

    public string Path { get; }

    public SqliteDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Database path must not be empty.", nameof(path));

        Path = System.IO.Path.GetFullPath(path);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path,
            ForeignKeys = true,
            DefaultTimeout = DefaultTimeoutSeconds,
            Pooling = true
        };

        connectionString = builder.ConnectionString;
    }

    public void Open()
    {
        lock (lifecycleGate)
        {
            ThrowIfDisposed();

            if (open)
                return;

            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var connection = CreateConnection();
            connection.Open();
            ConfigureBootstrapConnection(connection);

            open = true;
        }
    }

    public void InitializeSchema()
    {
        EnterWriterGate();

        try
        {
            using var connection = AcquireConnection();
            using var transaction = connection.BeginTransaction(deferred: false);

            try
            {
                ExecuteNonQuery(
                    connection,
                    transaction,
                    """
                    CREATE TABLE IF NOT EXISTS schema_meta(
                        key TEXT PRIMARY KEY,
                        value TEXT NOT NULL
                    );
                    """);

                var version = ReadSchemaVersion(connection, transaction);

                if (version > SchemaVersion)
                {
                    throw new SqliteSchemaException(
                        $"Database schema version {version} is newer than supported version {SchemaVersion}.");
                }

                while (version < SchemaVersion)
                {
                    version = ApplyNextMigration(connection, transaction, version);
                    SetSchemaVersion(connection, transaction, version);
                }

                transaction.Commit();
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                }

                throw;
            }
        }
        finally
        {
            writerGate.Release();
        }
    }

    public int Execute(
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        EnterWriterGate();

        try
        {
            using var connection = AcquireConnection();
            using var command = CreateCommand(connection, sql, null, parameters);
            return command.ExecuteNonQuery();
        }
        finally
        {
            writerGate.Release();
        }
    }

    public object? ExecuteScalar(
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        EnterWriterGate();

        try
        {
            using var connection = AcquireConnection();
            using var command = CreateCommand(connection, sql, null, parameters);
            var value = command.ExecuteScalar();

            return value is DBNull ? null : value;
        }
        finally
        {
            writerGate.Release();
        }
    }

    public object? QueryScalar(
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);

        using var connection = AcquireConnection();
        using var command = CreateCommand(connection, sql, null, parameters);
        var value = command.ExecuteScalar();

        return value is DBNull ? null : value;
    }

    public void ExecuteTransaction(
        Action<SqliteConnection, SqliteTransaction> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        EnterWriterGate();

        try
        {
            using var connection = AcquireConnection();
            using var transaction = connection.BeginTransaction(deferred: false);

            try
            {
                operation(connection, transaction);
                transaction.Commit();
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                }

                throw;
            }
        }
        finally
        {
            writerGate.Release();
        }
    }

    public void Close()
    {
        lock (lifecycleGate)
            open = false;
    }

    public void Dispose()
    {
        lock (lifecycleGate)
        {
            if (disposed)
                return;

            disposed = true;
            open = false;
        }
    }

    private void EnterWriterGate()
    {
        ThrowIfNotOpen();

        writerGate.Wait();

        try
        {
            ThrowIfNotOpen();
        }
        catch
        {
            writerGate.Release();
            throw;
        }
    }

    private SqliteConnection AcquireConnection()
    {
        lock (lifecycleGate)
        {
            ThrowIfDisposed();

            if (!open)
                throw new InvalidOperationException("SQLite database is not open.");

            var connection = CreateConnection();
            try
            {
                connection.Open();
                ConfigureOperationConnection(connection);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
    }

    private SqliteConnection CreateConnection() =>
        new(connectionString);

    private static void ConfigureBootstrapConnection(SqliteConnection connection)
    {
        var journalMode = ExecuteScalar(connection, "PRAGMA journal_mode=WAL");
        if (!string.Equals(
                Convert.ToString(journalMode, CultureInfo.InvariantCulture),
                "wal",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SqliteException(
                "SQLite did not enter WAL journal mode.",
                0);
        }

        ConfigureOperationConnection(connection);
    }

    private static void ConfigureOperationConnection(SqliteConnection connection)
    {
        ExecuteNonQuery(connection, "PRAGMA synchronous=FULL");

        var foreignKeys = ExecuteScalar(connection, "PRAGMA foreign_keys");
        if (Convert.ToInt64(foreignKeys, CultureInfo.InvariantCulture) != 1)
            throw new SqliteException("SQLite foreign keys are not enabled.", 0);

        var synchronous = ExecuteScalar(connection, "PRAGMA synchronous");
        if (Convert.ToInt64(synchronous, CultureInfo.InvariantCulture) != 2)
            throw new SqliteException("SQLite synchronous mode is not FULL.", 0);
    }

    private static int ApplyNextMigration(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int currentVersion) =>
        currentVersion switch
        {
            0 => 1,
            _ => throw new SqliteSchemaException(
                $"No migration exists from schema version {currentVersion}.")
        };

    private static int ReadSchemaVersion(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT value FROM schema_meta WHERE key = 'schema_version' LIMIT 1";

        var value = command.ExecuteScalar();
        if (value is null || value is DBNull)
            return 0;

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (!int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var version) ||
            version < 0)
        {
            throw new SqliteSchemaException(
                $"Invalid database schema version '{text}'.");
        }

        return version;
    }

    private static void SetSchemaVersion(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int version)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            """
            INSERT INTO schema_meta(key, value)
            VALUES('schema_version', $version)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            ("$version", version.ToString(CultureInfo.InvariantCulture)));
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        string sql,
        SqliteTransaction? transaction,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;

        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);

        return command;
    }

    private static int ExecuteNonQuery(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, sql, null, parameters);
        return command.ExecuteNonQuery();
    }

    private static int ExecuteNonQuery(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, sql, transaction, parameters);
        return command.ExecuteNonQuery();
    }

    private static object? ExecuteScalar(
        SqliteConnection connection,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();

        return value is DBNull ? null : value;
    }

    private void ThrowIfNotOpen()
    {
        lock (lifecycleGate)
        {
            ThrowIfDisposed();

            if (!open)
                throw new InvalidOperationException("SQLite database is not open.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(SqliteDatabase));
    }
}
