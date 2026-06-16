using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace RushOrder.Desktop.Services;

public sealed record UpdateInfo(
    string Version,
    string DownloadUrl,
    string Changelog,
    bool   IsCritical);

public sealed class UpdateService : IDisposable
{
    // Change to the actual GitHub repo once published
    private const string ReleasesUrl =
        "https://api.github.com/repos/noahleceguic/rush-order/releases/latest";

    private readonly ILogger<UpdateService> _logger;
    private readonly HttpClient _http;

    public UpdateInfo? PendingUpdate   { get; private set; }
    public bool        IsDownloading   { get; private set; }

    public event Action<UpdateInfo>? UpdateAvailable;
    public event Action<int>?        DownloadProgress; // 0-100
    public event Action?             InstallReady;

    private string? _installerPath;

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
        _http   = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "RushOrder-Desktop/1.0");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            var json    = await _http.GetStringAsync(ReleasesUrl);
            var release = JsonConvert.DeserializeObject<GhRelease>(json);
            if (release is null) return;

            var tagVersion = release.TagName.TrimStart('v');
            if (!Version.TryParse(tagVersion, out var latest)) return;

            var current = Assembly.GetExecutingAssembly().GetName().Version
                          ?? new Version(1, 0, 0, 0);

            if (latest <= current) return;

            var asset = release.Assets?
                .FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            var isCritical = release.TagName.Contains("security", StringComparison.OrdinalIgnoreCase)
                          || release.TagName.Contains("critical", StringComparison.OrdinalIgnoreCase)
                          || (release.Body?.Contains("[CRÍTICA]", StringComparison.OrdinalIgnoreCase) ?? false);

            PendingUpdate = new UpdateInfo(
                tagVersion,
                asset?.BrowserDownloadUrl ?? string.Empty,
                release.Body ?? "Ver changelog en GitHub.",
                isCritical);

            UpdateAvailable?.Invoke(PendingUpdate);
            _logger.LogInformation("Update available: v{Version} (critical={Critical})",
                tagVersion, isCritical);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Update check skipped (unreachable or not configured)");
        }
    }

    public async Task DownloadAsync()
    {
        if (PendingUpdate is null || string.IsNullOrEmpty(PendingUpdate.DownloadUrl)) return;
        if (IsDownloading) return;

        IsDownloading = true;
        try
        {
            var dest = Path.Combine(Path.GetTempPath(), "RushOrderSetup.exe");
            using var response = await _http.GetAsync(
                PendingUpdate.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total  = response.Content.Headers.ContentLength ?? -1L;
            var buffer = new byte[65_536];
            long done  = 0;

            await using var src  = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(dest);
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                if (total > 0)
                    DownloadProgress?.Invoke((int)(done * 100 / total));
            }

            _installerPath = dest;
            DownloadProgress?.Invoke(100);
            InstallReady?.Invoke();
            _logger.LogInformation("Installer downloaded to {Path}", dest);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update download failed");
        }
        finally
        {
            IsDownloading = false;
        }
    }

    public void ApplyUpdate()
    {
        if (_installerPath is null || !File.Exists(_installerPath)) return;
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(_installerPath, "/SILENT")
            {
                UseShellExecute = true
            });
        Application.Exit();
    }

    public void Dispose() => _http.Dispose();

    // ── GitHub API shapes ─────────────────────────────────────────────────────

    private sealed class GhRelease
    {
        [JsonProperty("tag_name")] public string TagName { get; set; } = "";
        [JsonProperty("body")]     public string? Body   { get; set; }
        [JsonProperty("assets")]   public GhAsset[]? Assets { get; set; }
    }

    private sealed class GhAsset
    {
        [JsonProperty("name")]                  public string Name               { get; set; } = "";
        [JsonProperty("browser_download_url")]  public string BrowserDownloadUrl { get; set; } = "";
    }
}
