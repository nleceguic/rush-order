using System.Security.Cryptography;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Infrastructure.Settings;

namespace RushOrder.Infrastructure.Identity;

public sealed class FileRsaKeyProvider : IRsaKeyProvider, IDisposable
{
    private readonly RSA _privateKey;
    private readonly RSA _publicKey;

    public FileRsaKeyProvider(JwtSettings settings)
    {
        var privateKeyPath = Path.GetFullPath(settings.PrivateKeyPath);
        var publicKeyPath = Path.GetFullPath(settings.PublicKeyPath);

        Directory.CreateDirectory(Path.GetDirectoryName(privateKeyPath)!);

        if (File.Exists(privateKeyPath) && File.Exists(publicKeyPath))
        {
            _privateKey = RSA.Create();
            _privateKey.ImportFromPem(File.ReadAllText(privateKeyPath));
            _publicKey = RSA.Create();
            _publicKey.ImportFromPem(File.ReadAllText(publicKeyPath));
        }
        else
        {
            _privateKey = RSA.Create(2048);
            File.WriteAllText(privateKeyPath, _privateKey.ExportRSAPrivateKeyPem());

            _publicKey = RSA.Create();
            _publicKey.ImportSubjectPublicKeyInfo(_privateKey.ExportSubjectPublicKeyInfo(), out _);
            File.WriteAllText(publicKeyPath, _publicKey.ExportSubjectPublicKeyInfoPem());
        }
    }

    public RSA GetPrivateKey() => _privateKey;
    public RSA GetPublicKey() => _publicKey;

    public void Dispose()
    {
        _privateKey.Dispose();
        _publicKey.Dispose();
    }
}
