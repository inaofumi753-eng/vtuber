using System.Windows;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow : Window
{
    public MainWindow(VtuberApplication application)
    {
        InitializeComponent();
        DataContext = new Vm(application);
    }
}

public sealed class Vm
{
    public Vm(VtuberApplication application)
    {
        ApplicationName = application.Config.Name;
        Title = ApplicationName;
        Status = $"Estado: {application.State.ToString().ToUpperInvariant()}";
    }

    public string ApplicationName { get; }
    public string Title { get; }
    public string Status { get; }
}
