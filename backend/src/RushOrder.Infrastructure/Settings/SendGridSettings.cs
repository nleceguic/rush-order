namespace RushOrder.Infrastructure.Settings;

public sealed class SendGridSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "no-reply@rushorder.es";
    public string FromName { get; set; } = "Rush Order";
}
