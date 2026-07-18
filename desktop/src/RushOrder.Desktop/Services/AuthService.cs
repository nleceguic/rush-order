using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using RushOrder.Desktop.State;

namespace RushOrder.Desktop.Services;

public sealed class AuthService
{
    private const string CredentialTarget = "RushOrder_RefreshToken";

    // Same host the rest of the desktop app's services point at. If the backend is
    // started via `docker-compose` instead of `dotnet run`, this needs to be 5000.
    private static readonly string CachedUserPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RushOrder", "lastuser.json");

    private readonly AppState _state;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5143") };

    public AuthService(AppState state) => _state = state;

    public async Task<LoginResult> LoginAsync(
        string email, string password, bool rememberMe, CancellationToken ct = default)
    {
        var body = JsonConvert.SerializeObject(new { email, password });
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync("/api/v1/auth/login",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
        }
        catch { return LoginResult.NetworkError; }

        if (!response.IsSuccessStatusCode) return LoginResult.InvalidCredentials;

        var json    = await response.Content.ReadAsStringAsync(ct);
        var payload = JsonConvert.DeserializeObject<ApiEnvelope<LoginData>>(json)?.Data;
        if (payload is null) return LoginResult.InvalidCredentials;

        if (payload.RequiresMfa)
            return LoginResult.MfaRequired(payload.TempToken!);

        await ApplyLoginResponseAsync(payload, rememberMe, ct);
        return LoginResult.Success;
    }

    public async Task<LoginResult> VerifyMfaAsync(
        string mfaToken, string totpCode, bool rememberMe, CancellationToken ct = default)
    {
        var body = JsonConvert.SerializeObject(new { tempToken = mfaToken, code = totpCode });
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync("/api/v1/auth/mfa/verify",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
        }
        catch { return LoginResult.NetworkError; }

        if (!response.IsSuccessStatusCode) return LoginResult.InvalidCredentials;

        var json    = await response.Content.ReadAsStringAsync(ct);
        var payload = JsonConvert.DeserializeObject<ApiEnvelope<LoginData>>(json)?.Data;
        if (payload is null) return LoginResult.InvalidCredentials;

        await ApplyLoginResponseAsync(payload, rememberMe, ct);
        return LoginResult.Success;
    }

    public async Task<bool> TryAutoLoginAsync(CancellationToken ct = default)
    {
        var token = ReadCredential(CredentialTarget);
        var cachedUser = ReadCachedUser();
        if (token is null || cachedUser is null) return false;

        var body = JsonConvert.SerializeObject(new { refreshToken = token });
        try
        {
            var response = await _http.PostAsync("/api/v1/auth/refresh",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);

            if (!response.IsSuccessStatusCode) return false;

            var json    = await response.Content.ReadAsStringAsync(ct);
            // /refresh only returns new tokens, not user info — reuse the cached identity.
            var payload = JsonConvert.DeserializeObject<ApiEnvelope<RefreshData>>(json)?.Data;
            if (payload is null) return false;

            _state.SetAuthenticated(cachedUser, payload.AccessToken, payload.RefreshToken);
            SaveCredential(CredentialTarget, payload.RefreshToken);
            await LoadCurrentRestaurantAsync(ct);
            return true;
        }
        catch { return false; }
    }

    public void Logout()
    {
        DeleteCredential(CredentialTarget);
        DeleteCachedUser();
        _state.Logout();
    }

    private async Task ApplyLoginResponseAsync(LoginData result, bool rememberMe, CancellationToken ct)
    {
        var user = result.User!;
        var initials = GetInitials(user.Email);
        var userInfo = new UserInfo(user.Id, user.Email, GetDisplayName(user.Email), user.Role, initials);

        _state.SetAuthenticated(userInfo, result.AccessToken!, result.RefreshToken!);

        if (rememberMe)
        {
            SaveCredential(CredentialTarget, result.RefreshToken!);
            SaveCachedUser(userInfo);
        }

        await LoadCurrentRestaurantAsync(ct);
    }

    // AppState.CurrentRestaurant has no other setter anywhere in the app — every
    // service that reads it (Dashboard, Menu, Orders, Tables, Kitchen, Statistics,
    // RealTime...) depends on this running once, right after authentication.
    private async Task LoadCurrentRestaurantAsync(CancellationToken ct)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _state.AccessToken);
            var response = await _http.GetAsync("/api/v1/restaurants", ct);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(ct);
            var restaurants = JsonConvert.DeserializeObject<ApiEnvelope<List<RestaurantData>>>(json)?.Data;
            var first = restaurants?.FirstOrDefault();
            if (first is not null)
                _state.SetRestaurant(new RestaurantInfo(first.Id, first.Name, first.LogoUrl));
        }
        catch { /* screens fall back to mock data individually; not fatal to login */ }
    }

    // Backend's login response has no display name (UserInfoDto only has Id/Email/Role/
    // Restaurants) — fall back to the email's local part.
    private static string GetDisplayName(string email) => email[..email.IndexOf('@')];

    // ── Local user-identity cache (for auto-login via refresh token) ───────────

    private static void SaveCachedUser(UserInfo user)
    {
        var dir = Path.GetDirectoryName(CachedUserPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(CachedUserPath, JsonConvert.SerializeObject(user));
    }

    private static UserInfo? ReadCachedUser()
    {
        if (!File.Exists(CachedUserPath)) return null;
        try { return JsonConvert.DeserializeObject<UserInfo>(File.ReadAllText(CachedUserPath)); }
        catch { return null; }
    }

    private static void DeleteCachedUser()
    {
        if (File.Exists(CachedUserPath)) File.Delete(CachedUserPath);
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

// Backend wraps every AuthController response in ApiResponse<T> ({status, data, ...}).
internal sealed record ApiEnvelope<T>(string Status, T? Data);

internal sealed record UserData(Guid Id, string Email, string Role, List<Guid>? Restaurants);

internal sealed record LoginData(
    bool      RequiresMfa,
    string?   TempToken,
    string?   AccessToken,
    string?   RefreshToken,
    DateTimeOffset? ExpiresAt,
    UserData? User);

internal sealed record RefreshData(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

internal sealed record RestaurantData(Guid Id, string Name, string? LogoUrl);

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
