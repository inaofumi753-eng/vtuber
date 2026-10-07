using Xunit;
using CoreTaskScheduler = VtuberBot.Core.TaskScheduler;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Reflection;
using VtuberBot.Core;

namespace VtuberBot.Tests;

public sealed class Tests
{
    [Fact]
    public void ConfigMissingFileUsesDefaults()
    {
        using var directory = new TempDirectory();
        var config = AppConfig.Load(Path.Combine(directory.Path, "missing.toml"));

        Assert.Equal("VTuber Bot", config.Name);
        Assert.Equal(Path.Combine(directory.Path, "data", "vtuber.sqlite3"), config.DatabasePath);
        Assert.Equal(LogLevel.Info, config.LogLevel);
        Assert.Null(config.ConfigPath);
    }

    [Fact]
    public void ConfigEmptyTomlUsesDefaults()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, string.Empty);

        var config = AppConfig.Load(path);

        Assert.Equal("VTuber Bot", config.Name);
        Assert.Equal(Path.Combine(directory.Path, "data", "vtuber.sqlite3"), config.DatabasePath);
        Assert.Equal(LogLevel.Info, config.LogLevel);
    }

    [Fact]
    public void ConfigOnlyDatabaseUsesOtherDefaults()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[database]\npath = \"data/custom.sqlite3\"");

        var config = AppConfig.Load(path);

        Assert.Equal("VTuber Bot", config.Name);
        Assert.Equal(Path.Combine(directory.Path, "data", "custom.sqlite3"), config.DatabasePath);
        Assert.Equal(LogLevel.Info, config.LogLevel);
    }

    [Fact]
    public void ConfigOnlyAppUsesOtherDefaults()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[app]\nname = \"Configured Bot\"");

        var config = AppConfig.Load(path);

        Assert.Equal("Configured Bot", config.Name);
        Assert.Equal(Path.Combine(directory.Path, "data", "vtuber.sqlite3"), config.DatabasePath);
        Assert.Equal(LogLevel.Info, config.LogLevel);
    }

    [Fact]
    public void ConfigOnlyLoggingUsesOtherDefaults()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[logging]\nlevel = \"DEBUG\"");

        var config = AppConfig.Load(path);

        Assert.Equal("VTuber Bot", config.Name);
        Assert.Equal(LogLevel.Debug, config.LogLevel);
    }

    [Fact]
    public void ConfigInvalidTomlThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[app\nname = \"broken\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigInvalidLoggingLevelThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[logging]\nlevel = \"NOPE\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigRelativePathUsesConfigDirectory()
    {
        using var directory = new TempDirectory();
        var path = directory.File("nested", "config.toml");
        File.WriteAllText(path, "[database]\npath = \"db/vtuber.sqlite3\"");

        var config = AppConfig.Load(path);

        Assert.Equal(
            Path.Combine(directory.Path, "nested", "db", "vtuber.sqlite3"),
            config.DatabasePath);
    }

    [Fact]
    public void ConfigWrongAppSectionTypeThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "app = \"not-a-table\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigWrongDatabaseSectionTypeThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "database = \"not-a-table\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigWrongLoggingSectionTypeThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "logging = \"not-a-table\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigEmptyDatabasePathThrows()
    {
        using var directory = new TempDirectory();
        var path = directory.File("config.toml");
        File.WriteAllText(path, "[database]\npath = \"\"");

        Assert.Throws<ConfigException>(() => AppConfig.Load(path));
    }

    [Fact]
    public void ConfigDefaultBaseDirectory()
    {
        var config = AppConfig.Load();

        Assert.True(File.Exists(Path.Combine(config.BaseDirectory, "PROJECT_SPEC.md")));
        Assert.Equal(
            Path.GetFullPath(Path.Combine(config.BaseDirectory, "data", "vtuber.sqlite3")),
            config.DatabasePath);
    }


    [Fact]
    public void DatabaseCreatesSchemaAndEnablesForeignKeys()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.InitializeSchema();

        using var command = database.Connection!.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys";
        Assert.Equal(1L, command.ExecuteScalar());

        command.CommandText = "SELECT value FROM schema_meta WHERE key = 'schema_version'";
        Assert.Equal(SqliteDatabase.SchemaVersion.ToString(), command.ExecuteScalar());
    }

    [Fact]
    public void DatabaseExecuteUsesParameters()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.Execute("CREATE TABLE sample(value TEXT NOT NULL)");
        Assert.Equal(
            1,
            database.Execute(
                "INSERT INTO sample(value) VALUES ($value)",
                ("$value", "hello")));

        using var command = database.Connection!.CreateCommand();
        command.CommandText = "SELECT value FROM sample";
        Assert.Equal("hello", command.ExecuteScalar());
    }

    [Fact]
    public void DatabaseCloseIsIdempotentAndAccessAfterCloseFails()
    {
        using var directory = new TempDirectory();
        var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();
        database.Close();
        database.Close();

        Assert.Throws<InvalidOperationException>(() => database.Execute("SELECT 1"));
    }

    [Fact]
    public void DatabaseSqlErrorIsReported()
    {
        using var directory = new TempDirectory();
        using var database = new SqliteDatabase(directory.File("a.db"));

        database.Open();

        Assert.Throws<SqliteException>(() => database.Execute("THIS IS NOT SQL"));
    }

    [Fact]
    public void EventsRejectInvalidNamesAndDuplicateHandlers()
    {
        var manager = new EventManager(new TestLogger());
        var calls = 0;
        Action<AppEvent> handler = _ => calls++;

        Assert.Throws<ArgumentException>(() => new AppEvent(" "));
        Assert.Throws<ArgumentException>(() => manager.Subscribe("", handler));

        manager.Subscribe("event", handler);
        manager.Subscribe("event", handler);
        manager.Emit(new AppEvent("event"));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void EventsUnsubscribeAndSnapshotAreSafe()
    {
        var manager = new EventManager(new TestLogger());
        var calls = 0;
        Action<AppEvent> handler = null!;
        handler = _ =>
        {
            calls++;
            manager.Unsubscribe("event", handler!);
        };

        manager.Subscribe("event", handler);
        manager.Emit(new AppEvent("event"));
        manager.Emit(new AppEvent("event"));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void EventsIsolateHandlerFailure()
    {
        var manager = new EventManager(new TestLogger());
        var calls = new List<string>();

        manager.Subscribe("event", _ =>
        {
            calls.Add("bad");
            throw new InvalidOperationException("expected");
        });
        manager.Subscribe("event", _ => calls.Add("good"));

        manager.Emit(new AppEvent("event"));

        Assert.Equal(["bad", "good"], calls);
    }

    [Fact]
    public void ResourcesCleanupInReverseOrderAndContinueAfterFailure()
    {
        var manager = new ResourceManager(new TestLogger());
        var calls = new List<string>();

        manager.Register("a", () => calls.Add("a"));
        manager.Register("b", () =>
        {
            calls.Add("b");
            throw new InvalidOperationException("expected");
        });
        manager.Register("c", () => calls.Add("c"));

        manager.CleanupAll();

        Assert.Equal(["c", "b", "a"], calls);
    }

    [Fact]
    public void ResourcesDuplicateNamesReplaceCleanup()
    {
        var manager = new ResourceManager(new TestLogger());
        var calls = new List<string>();

        manager.Register("same", () => calls.Add("first"));
        manager.Register("same", () => calls.Add("second"));
        manager.CleanupAll();

        Assert.Equal(["second"], calls);
    }

    [Fact]
    public void ResourcesRejectInvalidNamesAndAreIdempotent()
    {
        var manager = new ResourceManager(new TestLogger());
        Assert.Throws<ArgumentException>(() => manager.Register(" ", () => { }));

        var calls = 0;
        manager.Register("a", () => calls++);
        manager.CleanupAll();
        manager.CleanupAll();

        Assert.Equal(1, calls);
        Assert.Throws<InvalidOperationException>(() => manager.Register("b", () => { }));
    }

    [Fact]
    public void SchedulerExecutesNormalCallback()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new ManualResetEventSlim();

        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(20), signal.Set);

        Assert.True(signal.Wait(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SchedulerCancellationPreventsCallback()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new ManualResetEventSlim();

        var id = scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(200), signal.Set);

        Assert.True(scheduler.Cancel(id));
        Assert.False(scheduler.Cancel(id));
        Assert.False(signal.Wait(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SchedulerSupportsMultipleTasks()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new CountdownEvent(3);

        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(10), () => signal.Signal());
        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(20), () => signal.Signal());
        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(30), () => signal.Signal());

        Assert.True(signal.Wait(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SchedulerIsolatesCallbackFailure()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new ManualResetEventSlim();

        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(10), () => throw new InvalidOperationException("expected"));
        scheduler.ScheduleOnce(TimeSpan.FromMilliseconds(20), signal.Set);

        Assert.True(signal.Wait(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SchedulerShutdownDiscardsPendingTasks()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var signal = new ManualResetEventSlim();

        scheduler.ScheduleOnce(TimeSpan.FromSeconds(1), signal.Set);
        scheduler.Shutdown();

        Assert.False(signal.IsSet);
        Assert.Throws<InvalidOperationException>(
            () => scheduler.ScheduleOnce(TimeSpan.Zero, signal.Set));
    }

    [Fact]
    public void SchedulerShutdownWaitsForRunningCallback()
    {
        using var scheduler = new CoreTaskScheduler(new TestLogger());
        using var started = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();

        scheduler.ScheduleOnce(TimeSpan.Zero, () =>
        {
            started.Set();
            Thread.Sleep(100);
            finished.Set();
        });

        Assert.True(started.Wait(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        scheduler.Shutdown();

        Assert.True(finished.IsSet);
    }

    [Fact]
    public void ApplicationLifecycleAndDoubleShutdown()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[database]\npath = \"a.db\"");

        var application = new VtuberApplication(configPath);
        application.Initialize();

        Assert.Equal(AppState.Running, application.State);
        Assert.Equal(1, application.InternalTestEventsProcessed);

        application.Shutdown();
        application.Shutdown();

        Assert.Equal(AppState.Stopped, application.State);
    }

    [Fact]
    public void ApplicationStartupFailureEntersErrorAndCanShutdown()
    {
        using var directory = new TempDirectory();
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, "[logging]\nlevel = \"NOPE\"");

        var application = new VtuberApplication(configPath);

        Assert.Throws<ApplicationException>(() => application.Initialize());
        Assert.Equal(AppState.Error, application.State);

        application.Shutdown();

        Assert.Equal(AppState.Stopped, application.State);
    }

    [Fact]
    public void ApplicationDatabaseStartupFailureCleansPartialResources()
    {
        using var directory = new TempDirectory();
        var databaseDirectory = directory.Directory("database");
        var configPath = directory.File("config.toml");
        File.WriteAllText(configPath, $"[database]\npath = \"{databaseDirectory.Replace("\\", "\\\\")}\"");

        var application = new VtuberApplication(configPath);

        Assert.Throws<ApplicationException>(() => application.Initialize());
        Assert.Equal(AppState.Error, application.State);

        application.Shutdown();

        Assert.Equal(AppState.Stopped, application.State);
    }

    [Fact]
    public void VoiceDefaultsAndValidation()
    {
        var request = new SpeechRequest("hello");

        Assert.Null(request.Voice);
        Assert.Equal(SpeechPriority.Normal, request.Priority);
        Assert.Throws<ArgumentException>(() => new SpeechRequest(" "));
    }

    [Fact]
    public async Task ProcessWaitAndStopCanRunConcurrently()
    {
        var process = CreateLongRunningProcess();
        try
        {
            process.Start();
            var waitTask = System.Threading.Tasks.Task.Run(() => process.Wait());

            Assert.True(
                SpinWait.SpinUntil(
                    () => GetActiveWaiters(process) > 0,
                    TimeSpan.FromSeconds(1)));

            var stopTask = System.Threading.Tasks.Task.Run(() => process.Stop());
            await stopTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            await waitTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.False(process.IsRunning);
        }
        finally
        {
            process.Stop();
        }
    }

    [Fact]
    public async Task ProcessWaitAndDisposeCanRunConcurrently()
    {
        var process = CreateLongRunningProcess();
        try
        {
            process.Start();
            var waitTask = System.Threading.Tasks.Task.Run(() => process.Wait());

            Assert.True(
                SpinWait.SpinUntil(
                    () => GetActiveWaiters(process) > 0,
                    TimeSpan.FromSeconds(1)));

            var disposeTask = System.Threading.Tasks.Task.Run(process.Dispose, TestContext.Current.CancellationToken);
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            await waitTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.False(process.IsRunning);
        }
        finally
        {
            process.Dispose();
        }
    }

    [Fact]
    public async Task ProcessWaitAndRestartCanRunConcurrently()
    {
        var process = CreateLongRunningProcess();
        try
        {
            var firstPid = process.Start();
            var waitTask = System.Threading.Tasks.Task.Run(() => process.Wait());

            Assert.True(
                SpinWait.SpinUntil(
                    () => GetActiveWaiters(process) > 0,
                    TimeSpan.FromSeconds(1)));

            var restartTask = System.Threading.Tasks.Task.Run(() => process.Restart());
            var secondPid = await restartTask.WaitAsync(
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);

            Assert.NotEqual(firstPid, secondPid);
            await waitTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(secondPid, process.Pid);
        }
        finally
        {
            process.Stop();
        }
    }


    [Fact]
    public void ProcessMissingCwd()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe"], "Z:\\missing-vtuber-dir"),
            new TestLogger());

        Assert.Throws<ProcessManagerException>(() => process.Start());
    }

    [Fact]
    public void ProcessDoubleStartIsRejected()
    {
        using var process = CreateLongRunningProcess();

        process.Start();
        Assert.Throws<ProcessManagerException>(() => process.Start());

        process.Stop();
    }

    [Fact]
    public async Task ProcessConcurrentStopIsSafe()
    {
        using var process = CreateLongRunningProcess();
        process.Start();

        var stops = Enumerable.Range(0, 2)
            .Select(_ => System.Threading.Tasks.Task.Run(() => process.Stop()))
            .ToArray();

        await System.Threading.Tasks.Task.WhenAll(stops);

        Assert.False(process.IsRunning);
        Assert.All(stops, task => Assert.Null(task.Exception));
    }

    [Fact]
    public void ProcessWaitAndNaturalExitReleaseProcess()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe", "/c", "exit", "7"]),
            new TestLogger());

        process.Start();

        Assert.Equal(7, process.Wait(TimeSpan.FromSeconds(3)));
        Assert.Null(process.Pid);
        Assert.False(process.IsRunning);
    }

    [Fact]
    public void ProcessStopAfterNaturalExitIsSafe()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe", "/c", "exit", "0"]),
            new TestLogger());

        process.Start();
        Assert.True(SpinWait.SpinUntil(() => !process.IsRunning, 1000));

        Assert.Equal(0, process.Stop());
        Assert.Null(process.Pid);
    }

    [Fact]
    public void ProcessRestartAfterExitWorks()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe", "/c", "exit", "0"]),
            new TestLogger());

        process.Start();
        Assert.True(SpinWait.SpinUntil(() => !process.IsRunning, 1000));

        process.Restart();

        Assert.NotNull(process.Pid);
        Assert.Equal(0, process.Wait(TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void ProcessExternalTerminationIsHandled()
    {
        using var process = CreateLongRunningProcess();
        var pid = process.Start();

        using (var external = Process.GetProcessById(pid))
            external.Kill();

        Assert.NotNull(process.Wait(TimeSpan.FromSeconds(3)));
        Assert.Null(process.Pid);
    }

    [Fact]
    public void ProcessRepeatedStartWaitDoesNotRetainPreviousInstances()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(["cmd.exe", "/c", "exit", "0"]),
            new TestLogger());

        for (var i = 0; i < 20; i++)
        {
            process.Start();
            Assert.Equal(0, process.Wait(TimeSpan.FromSeconds(3)));
        }

        Assert.Null(process.Pid);
    }

    [Fact]
    public void ProcessEnvironmentOverridesArePassedToChild()
    {
        using var process = new OwnedProcess(
            new ProcessSpec(
                ["cmd.exe", "/c", "if \"%VTUBER_TEST_ENV%\"==\"expected\" (exit /b 0) else (exit /b 9)"],
                Environment: new Dictionary<string, string>
                {
                    ["VTUBER_TEST_ENV"] = "expected"
                }),
            new TestLogger());

        process.Start();

        Assert.Equal(0, process.Wait(TimeSpan.FromSeconds(3)));
        Assert.Null(process.Pid);
    }

    [Fact]
    public void StopRejectsNegativeTimeout()
    {
        using var process = CreateLongRunningProcess();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => process.Stop(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WaitRejectsNegativeTimeout()
    {
        using var process = CreateLongRunningProcess();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => process.Wait(TimeSpan.FromSeconds(-1)));
    }


    [Fact]
    public void ProcessDisposeIsIdempotent()
    {
        using var process = CreateLongRunningProcess();
        process.Start();

        process.Dispose();
        process.Dispose();

        Assert.False(process.IsRunning);
        Assert.Null(process.Pid);
    }

    [Fact]
    public void LoggingWritesFileAndDisposes()
    {
        using var directory = new TempDirectory();
        var logger = new Logger(directory.Path, LogLevel.Info);

        logger.Info("hello");
        logger.Warning("warning");
        logger.Dispose();
        logger.Dispose();

        var path = Path.Combine(directory.Path, "vtuber.log");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("hello", text);
        Assert.Contains("warning", text);
    }

    [Fact]
    public void LoggingRotatesAtApproximatelyOneMegabyte()
    {
        using var directory = new TempDirectory();
        using var logger = new Logger(directory.Path, LogLevel.Info);
        var payload = new string('x', 1200);

        for (var i = 0; i < 1100; i++)
            logger.Info("{0}", payload);

        var active = new FileInfo(Path.Combine(directory.Path, "vtuber.log"));
        var backup = new FileInfo(Path.Combine(directory.Path, "vtuber.log.1"));

        Assert.True(active.Exists);
        Assert.True(backup.Exists);
        Assert.True(backup.Length >= 1_000_000);
    }

    private static int GetActiveWaiters(OwnedProcess process)
    {
        var field = typeof(OwnedProcess).GetField(
            "activeWaiters",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (int)field!.GetValue(process)!;
    }

    private static OwnedProcess CreateLongRunningProcess() =>
        new(
            new ProcessSpec(
                ["cmd.exe", "/c", "timeout", "/t", "5", "/nobreak", ">nul"]),
            new TestLogger());

    private sealed class TestLogger : ILogger
    {
        public void Info(string message, params object?[] args)
        {
        }

        public void Warning(string message, params object?[] args)
        {
        }

        public void Error(Exception exception, string message, params object?[] args)
        {
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Directory.CreateTempSubdirectory("vtuber-tests-").FullName;
        }

        public string Path { get; }

        public string File(params string[] parts)
        {
            var path = System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            return path;
        }

        public string Directory(string name)
        {
            var path = System.IO.Path.Combine(Path, name);
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
