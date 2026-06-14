using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using RushOrder.Application.Auth.DTOs;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Auth.Commands;

public record MfaSetupCommand(Guid UserId) : ICommand<MfaSetupResult>;

public sealed class MfaSetupCommandValidator : AbstractValidator<MfaSetupCommand>
{
    public MfaSetupCommandValidator() => RuleFor(x => x.UserId).NotEmpty();
}

public sealed class MfaSetupCommandHandler : IRequestHandler<MfaSetupCommand, MfaSetupResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public MfaSetupCommandHandler(
        IUserRepository userRepository,
        ITotpService totpService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _totpService = totpService;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task<MfaSetupResult> Handle(MfaSetupCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        var secret = _totpService.GenerateSecret();
        var issuer = _configuration["Jwt:Issuer"] ?? "RushOrder";
        var uri = _totpService.BuildOtpAuthUri(secret, issuer, user.Email.Value);
        var qrBase64 = _totpService.GenerateQrCodeBase64(uri);

        var backupCodes = GenerateBackupCodes(10);
        var backupHashes = backupCodes
            .Select(c => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(c))).ToLowerInvariant())
            .ToList()
            .AsReadOnly();

        user.SetPendingMfaSecret(secret);

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MfaSetupResult(qrBase64, secret, backupCodes);
    }

    private static IReadOnlyList<string> GenerateBackupCodes(int count)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return Enumerable.Range(0, count)
            .Select(_ => new string(Enumerable.Range(0, 8)
                .Select(__ => chars[RandomNumberGenerator.GetInt32(chars.Length)])
                .ToArray()))
            .ToList()
            .AsReadOnly();
    }
}
