using System.Diagnostics;

namespace VtuberBot.Core;

public sealed record ProcessSpec(IReadOnlyList<string> Command, string? WorkingDirectory = null)
{
    public ProcessSpec(IEnumerable<string> command, string? workingDirectory = null)
        : this(command.ToArray(), workingDirectory)
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
    private readonly object gate = new();
    private readonly ProcessSpec spec;
    private readonly ILogger logger;
    private Process? process;

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
        lock (gate)
            return StartLocked();
    }

    public int? Wait(TimeSpan? timeout = null)
    {
        lock (gate)
        {
            if (process is null)
                return null;

            var current = process;

            if (timeout is null)
            {
                current.WaitForExit();
            }
            else if (!current.WaitForExit(timeout.Value))
            {
                return null;
            }

            return ReleaseExitedProcess(current);
        }
    }

    public int? Stop(TimeSpan? timeout = null)
    {
        lock (gate)
            return StopLocked(timeout);
    }

    public int Restart(TimeSpan? timeout = null)
    {
        lock (gate)
        {
            StopLocked(timeout);
            return StartLocked();
        }
    }

    public void Dispose()
    {
        lock (gate)
            StopLocked(TimeSpan.FromSeconds(2));
    }

    private int? StopLocked(TimeSpan? timeout)
    {
        if (process is null)
            return null;

        var current = process;

        try
        {
            if (!current.HasExited)
            {
                try
                {
                    current.Kill(entireProcessTree: false);
                }
                catch (InvalidOperationException)
                {
                    // The process exited between HasExited and Kill.
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
            }

            return ReleaseExitedProcess(current);
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
