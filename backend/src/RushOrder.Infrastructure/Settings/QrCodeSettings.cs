using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Settings;

public sealed class QrCodeSettings : IQrCodeSettings
{
    public string BaseUrl { get; set; } = "https://app.rushorder.es/menu";
}
