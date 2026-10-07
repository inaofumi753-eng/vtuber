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
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            viewModel.Application.Initialize();
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

    public Vm(VtuberApplication application)
    {
        this.application = application;
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
        application.State is AppState.Stopped or AppState.Error;

    public bool CanStop =>
        application.State != AppState.Stopped;

    public bool CanExecute =>
        application.State == AppState.Running;

    public ObservableCollection<string> Activity { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        if (application.State != AppState.Stopped)
            ApplicationName = application.Config.Name;

        Status = $"Estado: {application.State.ToString().ToUpperInvariant()}";
        Title = ApplicationName;
        Raise(nameof(CanStart));
        Raise(nameof(CanStop));
        Raise(nameof(CanExecute));
    }

    public void AddActivity(string message)
    {
        Activity.Insert(0, message);
        while (Activity.Count > 20)
            Activity.RemoveAt(Activity.Count - 1);
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
