using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RushOrder.Desktop.Avalonia.Navigation;
using RushOrder.Desktop.Avalonia.Services;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard;
using RushOrder.Desktop.Avalonia.ViewModels.Dashboard.AiDashboard;
using RushOrder.Desktop.Avalonia.ViewModels.Shell;
using RushOrder.Desktop.Avalonia.Views.Shell;

namespace RushOrder.Desktop.Avalonia;

public sealed partial class App : Application
{
    private IHost? _host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton<AppState>();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<DashboardDataService>();
                    services.AddSingleton<ForecastDataService>();
                    services.AddSingleton<RealTimeService>();

                    services.AddTransient<RevenueWidgetViewModel>();
                    services.AddTransient<ActiveOrdersWidgetViewModel>();
                    services.AddTransient<TablesWidgetViewModel>();
                    services.AddTransient<AvgTicketWidgetViewModel>();
                    services.AddTransient<AlertsWidgetViewModel>();
                    services.AddTransient<ReservationsWidgetViewModel>();
                    services.AddTransient<DashboardViewModel>();

                    services.AddTransient<TodayForecastWidgetViewModel>();
                    services.AddTransient<SuggestionOfTheDayWidgetViewModel>();
                    services.AddTransient<KitchenEtaWidgetViewModel>();
                    services.AddTransient<AiDashboardViewModel>();

                    services.AddSingleton<MainWindowViewModel>();
                })
                .Build();

            var sp = _host.Services;
            var nav = sp.GetRequiredService<INavigationService>();

            // Re-points the shell's "dashboard"/"panel-ia" routes at the real modules,
            // replacing the PlaceholderView fallback from Task 6/7. AlertsWidgetViewModel is
            // resolved twice — once per Dashboard, once per Panel IA — each a fully
            // independent instance with its own LoadAsync/state, per Task 25's interface note.
            nav.Register("dashboard", _ => sp.GetRequiredService<DashboardViewModel>());
            nav.Register("panel-ia", _ => sp.GetRequiredService<AiDashboardViewModel>());

            desktop.MainWindow = new MainWindow { DataContext = sp.GetRequiredService<MainWindowViewModel>() };
            desktop.Exit += (_, _) => _host.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
