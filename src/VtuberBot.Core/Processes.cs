using System.Diagnostics;

namespace VtuberBot.Core;

public sealed record ProcessSpec(
    IReadOnlyList<string> Command,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    public ProcessSpec(
        IEnumerable<string> command,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null)
        : this(command.ToArray(), workingDirectory, environment)
    {
        if (Command.Count == 0 || string.IsNullOrWhiteSpace(Command[0]))
            throw new ArgumentException("Command must not be empty.", nameof(command));
    }
}

public sealed class ProcessManagerException : Exception
{
    public ProcessManagerException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class OwnedProcess : IDisposable
{
    private readonly object lifecycleGate = new();
    private readonly object gate = new();
    private readonly ManualResetEventSlim waitersIdle = new(true);
    private readonly ProcessSpec spec;
    private readonly ILogger logger;
    private Process? process;
    private int activeWaiters;

    public OwnedProcess(ProcessSpec spec, ILogger logger)
    {
        this.spec = spec;
        this.logger = logger;
    }

    public int? Pid
    {
        get
        {
            lock (gate)
                return process?.Id;
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (gate)
            {
                if (process is null)
                    return false;

                try
                {
                    return !process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }
    }

    public int Start()
    {
        lock (lifecycleGate)
        {
            Process? current;

            lock (gate)
            {
                if (process is null)
                    return StartLocked();

                current = process;
                if (!current.HasExited)
                    throw new ProcessManagerException("Process already running.");
            }

            waitersIdle.Wait();

            lock (gate)
            {
                if (ReferenceEquals(process, current) && activeWaiters == 0)
                    ReleaseExitedProcess(current);

                return StartLocked();
            }
        }
    }

    public int? Wait(TimeSpan? timeout = null)
    {
        if (timeout is not null && timeout.Value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        Process current;

        lock (lifecycleGate)
        {
            lock (gate)
            {
                if (process is null)
                    return null;

                current = process;
                activeWaiters++;
                if (activeWaiters == 1)
                    waitersIdle.Reset();
            }
        }

        var completed = false;

        try
        {
            if (timeout is null)
            {
                current.WaitForExit();
                completed = true;
            }
            else
            {
                completed = current.WaitForExit(timeout.Value);
            }

            if (!completed)
                return null;

            return current.ExitCode;
        }
        finally
        {
            lock (gate)
            {
                activeWaiters--;

                if (activeWaiters == 0)
                {
                    waitersIdle.Set();

                    if (completed && ReferenceEquals(process, current))
                        ReleaseExitedProcess(current);
                }
            }
        }
    }

    public int? Stop(TimeSpan? timeout = null)
    {
        if (timeout is not null && timeout.Value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        lock (lifecycleGate)
            return StopLocked(timeout);
    }

    public int Restart(TimeSpan? timeout = null)
    {
        if (timeout is not null && timeout.Value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        lock (lifecycleGate)
        {
            StopLocked(timeout);
            return StartLocked();
        }
    }

    public void Dispose()
    {
        lock (lifecycleGate)
            StopLocked(TimeSpan.FromSeconds(2));
    }

    private int? StopLocked(TimeSpan? timeout)
    {
        Process current;
        int? alreadyExitedCode = null;

        lock (gate)
        {
            if (process is null)
                return null;

            current = process;

            try
            {
                if (current.HasExited)
                {
                    alreadyExitedCode = current.ExitCode;
                }
                else
                {
                    try
                    {
                        current.Kill(entireProcessTree: false);
                    }
                    catch (InvalidOperationException)
                    {
                        // The process exited between HasExited and Kill.
                    }
                }
            }
            catch (InvalidOperationException exception)
            {
                throw new ProcessManagerException("Process lifecycle operation failed.", exception);
            }
        }

        try
        {
            if (alreadyExitedCode is not null)
            {
                waitersIdle.Wait();

                lock (gate)
                {
                    if (ReferenceEquals(process, current) && activeWaiters == 0)
                        ReleaseExitedProcess(current);
                }

                return alreadyExitedCode.Value;
            }

            var waitTimeout = timeout ?? TimeSpan.FromSeconds(5);
            if (!current.WaitForExit(waitTimeout))
            {
                try
                {
                    current.Kill(entireProcessTree: false);
                }
                catch (InvalidOperationException)
                {
                    // The process exited before the second kill.
                }

                current.WaitForExit();
            }

            var exitCode = current.ExitCode;
            waitersIdle.Wait();

            lock (gate)
            {
                if (ReferenceEquals(process, current) && activeWaiters == 0)
                    ReleaseExitedProcess(current);
            }

            return exitCode;
        }
        catch (InvalidOperationException exception)
        {
            throw new ProcessManagerException("Process lifecycle operation failed.", exception);
        }
    }

    private int ReleaseExitedProcess(Process current)
    {
        try
        {
            var exitCode = current.ExitCode;
            if (ReferenceEquals(process, current))
                process = null;

            current.Dispose();
            return exitCode;
        }
        catch
        {
            if (ReferenceEquals(process, current))
                process = null;

            current.Dispose();
            throw;
        }
    }

    private int StartLocked()
    {
        if (process is not null)
        {
            if (!process.HasExited)
                throw new ProcessManagerException("Process already running.");

            process.Dispose();
            process = null;
        }

        if (spec.WorkingDirectory is not null && !Directory.Exists(spec.WorkingDirectory))
            throw new ProcessManagerException("Working directory does not exist.");

        var startInfo = new ProcessStartInfo
        {
            FileName = spec.Command[0],
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (spec.WorkingDirectory is not null)
            startInfo.WorkingDirectory = spec.WorkingDirectory;

        if (spec.Environment is not null)
        {
            foreach (var entry in spec.Environment)
                startInfo.Environment[entry.Key] = entry.Value;
        }

        for (var index = 1; index < spec.Command.Count; index++)
            startInfo.ArgumentList.Add(spec.Command[index]);

        var startedProcess = new Process { StartInfo = startInfo };

        try
        {
            if (!startedProcess.Start())
                throw new ProcessManagerException("Process failed to start.");
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            startedProcess.Dispose();
            throw new ProcessManagerException("Process failed to start.", exception);
        }

        process = startedProcess;
        logger.Info("Owned process started: pid={0}", startedProcess.Id);
        return startedProcess.Id;
    }
}
