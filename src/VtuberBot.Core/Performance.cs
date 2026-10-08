using System.Diagnostics;

namespace VtuberBot.Core;

public enum PerformanceProfile
{
    Ahorro,
    Equilibrado,
    Calidad
}

public enum HealthState
{
    OK,
    DEGRADED,
    UNAVAILABLE
}

public sealed record PerformancePolicy
{
    public PerformancePolicy(PerformanceProfile profile)
    {
        Profile = profile;
        SamplingInterval = GetSamplingInterval(profile);
    }

    public PerformanceProfile Profile { get; }
    public TimeSpan SamplingInterval { get; }

    public static TimeSpan GetSamplingInterval(PerformanceProfile profile) =>
        profile switch
        {
            PerformanceProfile.Ahorro => TimeSpan.FromSeconds(5),
            PerformanceProfile.Equilibrado => TimeSpan.FromSeconds(2),
            PerformanceProfile.Calidad => TimeSpan.FromSeconds(1),
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
}

public sealed record HealthIssue(
    string Source,
    string Message,
    bool Optional = false);

public sealed record GpuPerformanceSample(
    double UtilizationPercent,
    long? DedicatedMemoryBytes = null);

public sealed record ProcessPerformanceSample(
    TimeSpan TotalProcessorTime,
    long WorkingSetBytes);

public sealed record HealthSnapshot
{
    public HealthSnapshot(
        DateTimeOffset timestamp,
        double? cpuPercent,
        long? workingSetBytes,
        GpuPerformanceSample? gpu,
        double? fps,
        PerformanceProfile profile,
        HealthState health,
        IReadOnlyList<HealthIssue>? issues = null)
    {
        Timestamp = timestamp;
        CpuPercent = cpuPercent;
        WorkingSetBytes = workingSetBytes;
        Gpu = gpu;
        Fps = fps;
        Profile = profile;
        Health = health;
        Issues = Array.AsReadOnly((issues ?? []).ToArray());
    }

    public DateTimeOffset Timestamp { get; }
    public double? CpuPercent { get; }
    public long? WorkingSetBytes { get; }
    public GpuPerformanceSample? Gpu { get; }
    public double? Fps { get; }
    public PerformanceProfile Profile { get; }
    public HealthState Health { get; }
    public IReadOnlyList<HealthIssue> Issues { get; }
}

public interface IPerformanceProbe
{
    ProcessPerformanceSample Sample();
}

public interface IGpuPerformanceProbe
{
    GpuPerformanceSample? Sample();
}

public interface IFpsSource
{
    double? Sample();
}

public interface IHealthClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IProcessPerformanceReader : IDisposable
{
    void Refresh();
    TimeSpan TotalProcessorTime { get; }
    long WorkingSet64 { get; }
}

public interface IPeriodicWaiter : IDisposable
{
    TimeSpan Period { get; set; }
    ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken);
}

public interface IPeriodicWaiterFactory
{
    IPeriodicWaiter Create(TimeSpan interval);
}

public sealed class SystemHealthClock : IHealthClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class PeriodicTimerFactory : IPeriodicWaiterFactory
{
    public IPeriodicWaiter Create(TimeSpan interval) =>
        new PeriodicTimerWaiter(new PeriodicTimer(interval));
}

public sealed class PeriodicTimerWaiter : IPeriodicWaiter
{
    private readonly PeriodicTimer timer;

    public PeriodicTimerWaiter(PeriodicTimer timer) =>
        this.timer = timer;

    public TimeSpan Period
    {
        get => timer.Period;
        set => timer.Period = value;
    }

    public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken) =>
        timer.WaitForNextTickAsync(cancellationToken);

    public void Dispose() => timer.Dispose();
}

public sealed class SystemProcessPerformanceReader : IProcessPerformanceReader
{
    private readonly Process process = Process.GetCurrentProcess();

    public void Refresh() => process.Refresh();

    public TimeSpan TotalProcessorTime => process.TotalProcessorTime;

    public long WorkingSet64 => process.WorkingSet64;

    public void Dispose() => process.Dispose();
}

public sealed class CurrentProcessPerformanceProbe : IPerformanceProbe, IDisposable
{
    private readonly IProcessPerformanceReader reader;

    public CurrentProcessPerformanceProbe()
        : this(new SystemProcessPerformanceReader())
    {
    }

    public CurrentProcessPerformanceProbe(IProcessPerformanceReader reader)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public ProcessPerformanceSample Sample()
    {
        reader.Refresh();

        return new(
            reader.TotalProcessorTime,
            reader.WorkingSet64);
    }

    public void Dispose() => reader.Dispose();
}

public sealed class NullGpuPerformanceProbe : IGpuPerformanceProbe
{
    public GpuPerformanceSample? Sample() => null;
}

public sealed class NullFpsSource : IFpsSource
{
    public double? Sample() => null;
}

public sealed class PerformanceMonitor : IDisposable
{
    private readonly object gate = new();
    private readonly IPerformanceProbe processProbe;
    private readonly IGpuPerformanceProbe gpuProbe;
    private readonly IFpsSource fpsSource;
    private readonly IHealthClock clock;
    private readonly IPeriodicWaiterFactory timerFactory;
    private readonly ILogger logger;

    private CancellationTokenSource? cancellation;
    private Task? worker;
    private HealthSnapshot? latestSnapshot;
    private PerformanceProfile profile = PerformanceProfile.Equilibrado;
    private ProcessPerformanceSample? previousProcessSample;
    private DateTimeOffset? previousSampleTime;
    private bool disposed;

    public PerformanceMonitor(
        IPerformanceProbe processProbe,
        IGpuPerformanceProbe gpuProbe,
        IFpsSource fpsSource,
        IHealthClock clock,
        IPeriodicWaiterFactory timerFactory,
        ILogger logger)
    {
        this.processProbe = processProbe ?? throw new ArgumentNullException(nameof(processProbe));
        this.gpuProbe = gpuProbe ?? throw new ArgumentNullException(nameof(gpuProbe));
        this.fpsSource = fpsSource ?? throw new ArgumentNullException(nameof(fpsSource));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsRunning
    {
        get
        {
            lock (gate)
                return worker is not null;
        }
    }

    public PerformanceProfile Profile
    {
        get
        {
            lock (gate)
                return profile;
        }
    }

    public PerformancePolicy Policy
    {
        get
        {
            lock (gate)
                return new PerformancePolicy(profile);
        }
    }

    public HealthSnapshot? LatestSnapshot =>
        Volatile.Read(ref latestSnapshot);

    public event Action<HealthSnapshot>? SnapshotPublished;

    public void SetProfile(PerformanceProfile newProfile)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            profile = newProfile;
        }
    }

    public void Start()
    {
        lock (gate)
        {
            ThrowIfDisposed();

            if (worker is not null)
                return;

            previousProcessSample = null;
            previousSampleTime = null;
            cancellation = new CancellationTokenSource();
            worker = RunAsync(cancellation.Token);
        }
    }

    public void Stop()
    {
        Task? workerToWait;

        lock (gate)
        {
            workerToWait = worker;
            if (workerToWait is null)
                return;

            cancellation!.Cancel();
        }

        try
        {
            workerToWait.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Performance monitor stopped with an error.");
        }
    }

    public void Dispose()
    {
        Task? workerToWait;

        lock (gate)
        {
            if (disposed)
                return;

            disposed = true;
            workerToWait = worker;
            cancellation?.Cancel();
        }

        if (workerToWait is not null)
        {
            try
            {
                workerToWait.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Performance monitor disposal failed.");
            }
        }

        if (processProbe is IDisposable disposableProbe)
            disposableProbe.Dispose();

        lock (gate)
        {
            cancellation?.Dispose();
            cancellation = null;
            worker = null;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = timerFactory.Create(Policy.SamplingInterval);

            while (!cancellationToken.IsCancellationRequested)
            {
                PublishSample();
                timer.Period = Policy.SamplingInterval;

                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Performance monitor worker failed.");
        }
        finally
        {
            lock (gate)
            {
                if (cancellation?.Token == cancellationToken)
                {
                    worker = null;
                    cancellation?.Dispose();
                    cancellation = null;
                }
            }
        }
    }

    private void PublishSample()
    {
        var timestamp = clock.UtcNow;
        ProcessPerformanceSample? currentProcess = null;
        double? cpuPercent = null;
        long? workingSetBytes = null;
        GpuPerformanceSample? gpu = null;
        double? fps = null;
        var issues = new List<HealthIssue>();

        try
        {
            currentProcess = processProbe.Sample();
            workingSetBytes = currentProcess.WorkingSetBytes;

            var previous = previousProcessSample;
            var previousTime = previousSampleTime;

            if (previous is not null && previousTime is not null)
            {
                cpuPercent = CalculateCpuPercent(
                    previous,
                    currentProcess,
                    previousTime.Value,
                    timestamp,
                    Environment.ProcessorCount);
            }

            previousProcessSample = currentProcess;
            previousSampleTime = timestamp;
        }
        catch (Exception exception)
        {
            issues.Add(new("CPU/RAM", exception.Message));
            previousProcessSample = null;
            previousSampleTime = null;
        }

        try
        {
            gpu = gpuProbe.Sample();
        }
        catch (Exception exception)
        {
            issues.Add(new("GPU", exception.Message, Optional: true));
        }

        try
        {
            fps = fpsSource.Sample();
        }
        catch (Exception exception)
        {
            issues.Add(new("FPS", exception.Message, Optional: true));
        }

        var health = currentProcess is null
            ? HealthState.UNAVAILABLE
            : issues.Any(issue => issue.Optional)
                ? HealthState.DEGRADED
                : HealthState.OK;

        var snapshot = new HealthSnapshot(
            timestamp,
            cpuPercent,
            workingSetBytes,
            gpu,
            fps,
            Profile,
            health,
            issues);

        Volatile.Write(ref latestSnapshot, snapshot);

        foreach (Action<HealthSnapshot> listener in SnapshotPublished?.GetInvocationList() ?? [])
        {
            try
            {
                listener(snapshot);
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Performance monitor snapshot listener failed.");
            }
        }
    }

    public static double? CalculateCpuPercent(
        ProcessPerformanceSample previous,
        ProcessPerformanceSample current,
        DateTimeOffset previousTimestamp,
        DateTimeOffset currentTimestamp,
        int processorCount)
    {
        if (processorCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(processorCount));

        var wallClockSeconds = (currentTimestamp - previousTimestamp).TotalSeconds;
        var cpuSeconds = (current.TotalProcessorTime - previous.TotalProcessorTime).TotalSeconds;

        if (wallClockSeconds <= 0 || cpuSeconds < 0)
            return null;

        var percent = cpuSeconds / wallClockSeconds / processorCount * 100;
        return Math.Clamp(percent, 0, 100);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(PerformanceMonitor));
    }
}

