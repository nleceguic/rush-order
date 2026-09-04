using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var nav = new Navigation.NavigationService();
            desktop.MainWindow = new Views.Shell.MainWindow
            {
                DataContext = new ViewModels.Shell.MainWindowViewModel(nav)
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
