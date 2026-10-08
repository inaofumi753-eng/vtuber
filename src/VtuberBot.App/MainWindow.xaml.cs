using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow : Window
{
    private readonly Vm viewModel;

    public MainWindow(VtuberApplication application)
    {
        InitializeComponent();
        viewModel = new(application);
        DataContext = viewModel;
        viewModel.AttachHealthEvents();
        Closed += (_, _) => viewModel.DetachHealthEvents();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            viewModel.Application.Initialize();
            viewModel.AttachHealthEvents();
            viewModel.AddActivity("Aplicación iniciada.");
        }
        catch (Exception exception)
        {
            viewModel.Result = exception.Message;
            viewModel.AddActivity($"ERROR de inicio: {exception.Message}");
        }

        viewModel.Refresh();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            viewModel.DetachHealthEvents();
            viewModel.Application.Shutdown();
            viewModel.Result = "Aplicación detenida.";
            viewModel.AddActivity("Aplicación detenida.");
        }
        catch (Exception exception)
        {
            viewModel.Result = exception.Message;
            viewModel.AddActivity($"ERROR de detención: {exception.Message}");
        }

        viewModel.Refresh();
    }

    private void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteCommand();
    }

    private void CommandInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        ExecuteCommand();
        e.Handled = true;
    }

    private void ProfileComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is not PerformanceProfile profile)
            return;

        try
        {
            viewModel.SetPerformanceProfile(profile);
        }
        catch (InvalidOperationException)
        {
            viewModel.Refresh();
        }
    }

    private void ExecuteCommand()
    {
        var input = CommandInput.Text;

        if (string.IsNullOrWhiteSpace(input))
        {
            viewModel.Result = "Ingrese un comando.";
            viewModel.AddActivity("ERROR: comando vacío.");
            return;
        }

        try
        {
            var result = viewModel.Application.ExecuteCommand(input);
            viewModel.Result = result.Message;
            viewModel.AddActivity($"{input}: {(result.Success ? "OK" : "ERROR")} — {result.Message}");
        }
        catch (Exception exception)
        {
            viewModel.Result = exception.Message;
            viewModel.AddActivity($"ERROR inesperado: {exception.Message}");
        }
    }
}

public sealed class Vm : INotifyPropertyChanged
{
    private readonly VtuberApplication application;
    private string applicationName = "VTuber Bot";
    private string status = string.Empty;
    private string result = string.Empty;
    private string title = "VTuber Bot";
    private double? cpuPercent;
    private long? workingSetBytes;
    private GpuPerformanceSample? gpu;
    private double? fps;
    private HealthState health = HealthState.UNAVAILABLE;
    private DateTimeOffset? lastSample;
    private PerformanceProfile selectedProfile = PerformanceProfile.Equilibrado;

    public Vm(VtuberApplication application)
    {
        this.application = application;
        selectedProfile = application.PerformanceProfile;
        Refresh();
    }

    public VtuberApplication Application => application;

    public string ApplicationName
    {
        get => applicationName;
        private set => SetField(ref applicationName, value);
    }

    public string Version => VtuberApplication.Version;

    public string Status
    {
        get => status;
        private set => SetField(ref status, value);
    }

    public string Result
    {
        get => result;
        set => SetField(ref result, value);
    }

    public string Title
    {
        get => title;
        private set => SetField(ref title, value);
    }

    public bool CanStart =>
        application.State == AppState.Stopped;

    public bool CanStop =>
        application.State != AppState.Stopped;

    public bool CanExecute =>
        application.State == AppState.Running;

    public bool CanSelectProfile =>
        application.State == AppState.Running;

    public IReadOnlyList<PerformanceProfile> Profiles { get; } =
        Enum.GetValues<PerformanceProfile>();

    public PerformanceProfile SelectedProfile
    {
        get => selectedProfile;
        private set => SetField(ref selectedProfile, value);
    }

    public string CpuDisplay =>
        cpuPercent is null ? "N/D" : $"{cpuPercent.Value:0.0} %";

    public string RamDisplay =>
        workingSetBytes is null
            ? "N/D"
            : $"{workingSetBytes.Value / 1024d / 1024d:0.0} MB";

    public string GpuDisplay =>
        gpu is null
            ? "N/D"
            : $"{gpu.UtilizationPercent:0.0} %";

    public string FpsDisplay =>
        fps is null ? "N/D" : $"{fps.Value:0.0}";

    public string HealthDisplay => health.ToString();

    public string SamplingIntervalDisplay =>
        PerformancePolicy.GetSamplingInterval(application.PerformanceProfile).TotalSeconds.ToString("0.#") + " s";

    public string LastSampleDisplay =>
        lastSample is null
            ? "N/D"
            : lastSample.Value.ToLocalTime().ToString("HH:mm:ss");

    public ObservableCollection<string> Activity { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        if (application.State != AppState.Stopped)
            ApplicationName = application.Config.Name;

        Status = $"Estado: {application.State.ToString().ToUpperInvariant()}";
        Title = ApplicationName;
        SelectedProfile = application.PerformanceProfile;
        ApplySnapshot(application.LatestHealth);

        Raise(nameof(CanStart));
        Raise(nameof(CanStop));
        Raise(nameof(CanExecute));
        Raise(nameof(CanSelectProfile));
        Raise(nameof(SamplingIntervalDisplay));
    }

    public void SetPerformanceProfile(PerformanceProfile profile)
    {
        application.SetPerformanceProfile(profile);
        SelectedProfile = profile;
        Raise(nameof(SamplingIntervalDisplay));
    }

    public void AttachHealthEvents()
    {
        if (application.State == AppState.Stopped)
            return;

        DetachHealthEvents();
        subscribedEvents = application.EventManager;
        healthHandler = OnHealthUpdated;
        subscribedEvents.Subscribe(VtuberApplication.HealthUpdatedEvent, healthHandler);
        ApplySnapshot(application.LatestHealth);
    }

    public void DetachHealthEvents()
    {
        if (subscribedEvents is not null && healthHandler is not null)
            subscribedEvents.Unsubscribe(VtuberApplication.HealthUpdatedEvent, healthHandler);

        subscribedEvents = null;
        healthHandler = null;
    }

    public void AddActivity(string message)
    {
        Activity.Insert(0, message);
        while (Activity.Count > 20)
            Activity.RemoveAt(Activity.Count - 1);
    }

    private EventManager? subscribedEvents;
    private Action<AppEvent>? healthHandler;

    private void OnHealthUpdated(AppEvent appEvent)
    {
        if (appEvent.Payload is not HealthSnapshot snapshot)
            return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            ApplySnapshot(snapshot);
        else
            dispatcher.BeginInvoke(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(HealthSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            cpuPercent = null;
            workingSetBytes = null;
            gpu = null;
            fps = null;
            health = HealthState.UNAVAILABLE;
            lastSample = null;
            Raise(nameof(CpuDisplay));
            Raise(nameof(RamDisplay));
            Raise(nameof(GpuDisplay));
            Raise(nameof(FpsDisplay));
            Raise(nameof(HealthDisplay));
            Raise(nameof(LastSampleDisplay));
            return;
        }

        cpuPercent = snapshot.CpuPercent;
        workingSetBytes = snapshot.WorkingSetBytes;
        gpu = snapshot.Gpu;
        fps = snapshot.Fps;
        health = snapshot.Health;
        lastSample = snapshot.Timestamp;
        SelectedProfile = snapshot.Profile;

        Raise(nameof(CpuDisplay));
        Raise(nameof(RamDisplay));
        Raise(nameof(GpuDisplay));
        Raise(nameof(FpsDisplay));
        Raise(nameof(HealthDisplay));
        Raise(nameof(LastSampleDisplay));
        Raise(nameof(SelectedProfile));
        Raise(nameof(SamplingIntervalDisplay));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        Raise(name);
    }

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new(name));
}
