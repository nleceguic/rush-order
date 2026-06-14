using System.Security.Cryptography;

namespace RushOrder.Application.Common.Interfaces;

public interface IRsaKeyProvider
{
    RSA GetPrivateKey();
    RSA GetPublicKey();
}
