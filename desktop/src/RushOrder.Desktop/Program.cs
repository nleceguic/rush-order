using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;
using RushOrder.Desktop.Data;
using RushOrder.Desktop.Forms;
using RushOrder.Desktop.Navigation;
using RushOrder.Desktop.Notifications;
using RushOrder.Desktop.Services;
using RushOrder.Desktop.State;
using RushOrder.Desktop.Theme;
using RushOrder.Desktop.Views.Dashboard;
using RushOrder.Desktop.Views.FloorPlan;
using RushOrder.Desktop.Views.Kitchen;
using RushOrder.Desktop.Views.Menu;
using RushOrder.Desktop.Views.Orders;
using RushOrder.Desktop.Views.Statistics;
using Serilog;

namespace RushOrder.Desktop;

static class Program
{
    // TEMPORARY: set to false to restore the normal login gate. Left on while iterating
    // on Dashboard/UI fixes so each rebuild doesn't require logging in again.
    private const bool SkipLoginForTesting = true;

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // TEMPORARY diagnostics: log UI-thread exceptions (e.g. from Paint handlers) instead
        // of showing the JIT-debugger dialog per occurrence, so repeated ones are all visible
        // in one log file instead of blocking on a dialog each time.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            Log.Error(e.Exception, "DEBUG unhandled UI exception");

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RushOrder", "logs");
        Directory.CreateDirectory(logDir);

        QuestPDF.Settings.License = LicenseType.Community;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDir, "rushorder-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("RushOrder Desktop starting");
            PoppinsFont.Initialize();

            var host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((_, services) =>
                {
                    // Infrastructure
                    services.AddSingleton<AppState>();
                    services.AddSingleton<ThemeManager>();
                    services.AddSingleton<NavigationService>();
                    services.AddSingleton<ToastNotificationManager>();
                    services.AddSingleton<AuthService>();
                    services.AddSingleton<RealTimeService>();
                    services.AddSingleton<DashboardDataService>();
                    services.AddSingleton<TableService>();
                    services.AddSingleton<ProductSearchService>();
                    services.AddSingleton<KitchenService>();
                    services.AddSingleton<MenuService>();

                    // Print & statistics
                    services.AddSingleton<PrintService>();
                    services.AddSingleton<StatisticsDataService>();
                    services.AddSingleton<ForecastDataService>();

                    // Auto-update
                    services.AddSingleton<UpdateService>();

                    // Offline support
                    services.AddSingleton<LocalDatabase>();
                    services.AddSingleton<SyncService>();
                    services.AddSingleton<ConnectivityMonitor>();
                    services.AddSingleton<OrderService>();

                    // Shell
                    services.AddSingleton<MainForm>();
                    services.AddTransient<LoginForm>();

                    // Views
                    services.AddTransient<DashboardView>();
                    services.AddTransient<FloorPlanView>();
                    services.AddTransient<OrdersView>();
                    services.AddTransient<KitchenDisplayForm>();
                    services.AddTransient<MenuManagementControl>();
                    services.AddTransient<StatisticsView>();
                    services.AddTransient<Views.Forecast.DemandForecastControl>();
                    services.AddTransient<Views.AiDashboard.AiDashboardView>();
                })
                .Build();

            var services = host.Services;
            ThemeManager.Initialize(services.GetRequiredService<ThemeManager>());

            // Block synchronously (not await) — this runs before Application.Run(), so
            // WindowsFormsSynchronizationContext isn't installed yet. An `await` here can
            // resume on a thread-pool thread instead of this STA thread, and everything built
            // afterward (MainForm, OrdersView's drag-and-drop, etc.) silently inherits that
            // wrong thread — it mostly works until an OLE call (RegisterDragDrop) enforces STA
            // and throws. GetResult() keeps the whole startup path on the real STA thread.
            if (!SkipLoginForTesting)
            {
                var auth = services.GetRequiredService<AuthService>();
                var autoLoggedIn = auth.TryAutoLoginAsync().GetAwaiter().GetResult();
                if (!autoLoggedIn)
                {
                    using var login = services.GetRequiredService<LoginForm>();
                    if (login.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }
                }
            }

            Application.Run(services.GetRequiredService<MainForm>());
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
