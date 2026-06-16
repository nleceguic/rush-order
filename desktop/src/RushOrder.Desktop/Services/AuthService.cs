using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class AuthService
{
    private const string CredentialTarget = "RushOrder_RefreshToken";

    private readonly AppState _state;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5000") };

    public AuthService(AppState state) => _state = state;

    public async Task<LoginResult> LoginAsync(
        string email, string password, bool rememberMe, CancellationToken ct = default)
    {
        var body = JsonConvert.SerializeObject(new { email, password });
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync("/api/auth/login",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
        }
        catch { return LoginResult.NetworkError; }

        if (!response.IsSuccessStatusCode) return LoginResult.InvalidCredentials;

        var json   = await response.Content.ReadAsStringAsync(ct);
        var result = JsonConvert.DeserializeObject<LoginResponse>(json)!;

        if (result.RequiresMfa)
            return LoginResult.MfaRequired(result.MfaToken!);

        ApplyLoginResponse(result, rememberMe);
        return LoginResult.Success;
    }

    public async Task<LoginResult> VerifyMfaAsync(
        string mfaToken, string totpCode, bool rememberMe, CancellationToken ct = default)
    {
        var body = JsonConvert.SerializeObject(new { mfaToken, totpCode });
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync("/api/auth/mfa/verify",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
        }
        catch { return LoginResult.NetworkError; }

        if (!response.IsSuccessStatusCode) return LoginResult.InvalidCredentials;

        var json   = await response.Content.ReadAsStringAsync(ct);
        var result = JsonConvert.DeserializeObject<LoginResponse>(json)!;

        ApplyLoginResponse(result, rememberMe);
        return LoginResult.Success;
    }

    public async Task<bool> TryAutoLoginAsync(CancellationToken ct = default)
    {
        var token = ReadCredential(CredentialTarget);
        if (token is null) return false;

        var body = JsonConvert.SerializeObject(new { refreshToken = token });
        try
        {
            var response = await _http.PostAsync("/api/auth/refresh",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);

            if (!response.IsSuccessStatusCode) return false;

            var json   = await response.Content.ReadAsStringAsync(ct);
            var result = JsonConvert.DeserializeObject<LoginResponse>(json)!;
            ApplyLoginResponse(result, rememberMe: true);
            return true;
        }
        catch { return false; }
    }

    public void Logout()
    {
        DeleteCredential(CredentialTarget);
        _state.Logout();
    }

    private void ApplyLoginResponse(LoginResponse result, bool rememberMe)
    {
        var initials = GetInitials(result.FullName);
        _state.SetAuthenticated(
            new UserInfo(result.UserId, result.Email, result.FullName, result.Role, initials),
            result.AccessToken, result.RefreshToken);

        if (rememberMe) SaveCredential(CredentialTarget, result.RefreshToken);
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : name[..Math.Min(2, name.Length)].ToUpper();
    }

    // ── Windows Credential Manager ────────────────────────────────────────

    private static void SaveCredential(string target, string secret)
    {
        var blob = Encoding.UTF8.GetBytes(secret);
        var ptr  = Marshal.AllocHGlobal(blob.Length);
        Marshal.Copy(blob, 0, ptr, blob.Length);
        var cred = new CREDENTIAL
        {
            Type             = 1,
            TargetName       = target,
            CredentialBlobSize = (uint)blob.Length,
            CredentialBlob   = ptr,
            Persist          = 2,
            UserName         = "RushOrder",
        };
        CredWrite(ref cred, 0);
        Marshal.FreeHGlobal(ptr);
    }

    private static string? ReadCredential(string target)
    {
        if (!CredRead(target, 1, 0, out var ptr)) return null;
        var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
        var blob = new byte[cred.CredentialBlobSize];
        Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
        CredFree(ptr);
        return Encoding.UTF8.GetString(blob);
    }

    private static void DeleteCredential(string target) => CredDelete(target, 1, 0);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref CREDENTIAL credential, [In] uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr cred);

    [DllImport("advapi32.dll")]
    private static extern void CredFree([In] IntPtr buffer);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint    Flags, Type;
        public string  TargetName;
        public string? Comment;
        public uint    LastWrittenLow, LastWrittenHigh;
        public uint    CredentialBlobSize;
        public IntPtr  CredentialBlob;
        public uint    Persist;
        public uint    AttributeCount;
        public IntPtr  Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
}

internal sealed record LoginResponse(
    Guid   UserId,
    string Email,
    string FullName,
    string Role,
    string AccessToken,
    string RefreshToken,
    bool   RequiresMfa = false,
    string? MfaToken   = null);

public sealed class LoginResult
{
    public bool   IsSuccess      { get; private init; }
    public bool   NeedsMfa       { get; private init; }
    public bool   IsNetworkError { get; private init; }
    public string? PendingMfaToken { get; private init; }

    public static readonly LoginResult Success            = new() { IsSuccess = true };
    public static readonly LoginResult InvalidCredentials = new();
    public static readonly LoginResult NetworkError       = new() { IsNetworkError = true };

    public static LoginResult MfaRequired(string mfaToken) =>
        new() { NeedsMfa = true, PendingMfaToken = mfaToken };
}
