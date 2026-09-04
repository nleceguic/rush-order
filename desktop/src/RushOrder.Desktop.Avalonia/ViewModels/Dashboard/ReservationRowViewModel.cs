using RushOrder.Desktop.Avalonia.Models;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class ReservationRowViewModel(ReservationDto reservation)
{
    public string CustomerName => reservation.Notes is { Length: > 0 }
        ? $"{reservation.CustomerName} — {reservation.Notes}"
        : reservation.CustomerName;
    public string TimeText => reservation.ReservationTime.LocalDateTime.ToString("HH:mm");
    public string DetailText
    {
        get
        {
            var minutes = (int)(reservation.ReservationTime - DateTimeOffset.Now).TotalMinutes;
            var until = minutes <= 0 ? "Ahora" : minutes < 60 ? $"en {minutes} min" : $"en {minutes / 60}h {minutes % 60:D2}m";
            return $"{reservation.PartySize} personas · {until}";
        }
    }
    public bool IsUrgent => (reservation.ReservationTime - DateTimeOffset.Now).TotalMinutes <= 30;
}
