using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class AlertRowViewModel(AlertDto alert)
{
    public AlertDto Source { get; } = alert;
    public string Message => alert.Message;
    public AlertSeverity Severity => alert.Severity;
    public string Age => FormatAge(alert.OccurredAt);

    private static string FormatAge(DateTimeOffset ts)
    {
        var age = DateTimeOffset.Now - ts;
        if (age.TotalMinutes < 1) return "Ahora mismo";
        if (age.TotalMinutes < 60) return $"Hace {(int)age.TotalMinutes} min";
        return $"Hace {(int)age.TotalHours} h";
    }
}
