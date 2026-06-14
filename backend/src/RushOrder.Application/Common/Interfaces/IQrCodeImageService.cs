namespace RushOrder.Application.Common.Interfaces;

public interface IQrCodeImageService
{
    byte[] GeneratePng(string content, int pixelsPerModule = 10);
}
