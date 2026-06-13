namespace RushOrder.Infrastructure.Settings;

public sealed class SmtpSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromName { get; set; } = "Rush Order";
    public string FromAddress { get; set; } = "noreply@rushorder.local";
    public bool UseSsl { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string AdminEmail { get; set; } = "admin@rushorder.local";
}
