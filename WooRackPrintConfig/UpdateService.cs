using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace WooRackPrintConfig;

public class UpdateInfo
{
    public string TagName { get; set; } = "";
    public Version LatestVersion { get; set; } = new(0, 0, 0);
    public Version CurrentVersion { get; set; } = new(0, 0, 0);
    public string DownloadUrl { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public bool IsUpdateAvailable => LatestVersion > CurrentVersion && !string.IsNullOrWhiteSpace(DownloadUrl);
}

public class UpdateService
{
    private const string RepoOwner = "brianstrate";
    private const string RepoName = "woorack-print-agent";
    private readonly HttpClient _http;

    public UpdateService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WooRackPrintAgent-Updater/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static Version GetCurrentAppVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(infoAttr))
        {
            var clean = infoAttr.Split('+')[0].Split('-')[0].TrimStart('v', 'V');
            if (Version.TryParse(clean, out var parsed))
                return NormalizeVersion(parsed);
        }

        var v = asm.GetName().Version ?? new Version(1, 0, 0);
        return NormalizeVersion(v);
    }

    private static Version NormalizeVersion(Version v)
    {
        return new Version(
            Math.Max(0, v.Major),
            Math.Max(0, v.Minor),
            Math.Max(0, v.Build));
    }

    public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var current = GetCurrentAppVersion();
        var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new UpdateInfo { CurrentVersion = current, LatestVersion = current };
        }
        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<GitHubReleaseDto>(cancellationToken: ct);
        if (release == null || string.IsNullOrWhiteSpace(release.TagName))
        {
            return new UpdateInfo { CurrentVersion = current, LatestVersion = current };
        }

        var cleanTag = release.TagName.TrimStart('v', 'V').Split('-')[0];
        if (!Version.TryParse(cleanTag, out var parsedLatest))
        {
            return new UpdateInfo { CurrentVersion = current, LatestVersion = current };
        }

        var latest = NormalizeVersion(parsedLatest);
        var installerAsset = release.Assets?.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

        return new UpdateInfo
        {
            TagName = release.TagName,
            CurrentVersion = current,
            LatestVersion = latest,
            DownloadUrl = installerAsset?.BrowserDownloadUrl ?? "",
            ReleaseNotes = release.Body ?? ""
        };
    }

    public async Task DownloadAndInstallAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
            throw new InvalidOperationException("Ingen .exe-installer fundet i GitHub Release.");

        var tempFile = Path.Combine(
            Path.GetTempPath(),
            $"WooRackPrintAgent-Setup-{update.TagName}.exe");

        using (var response = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                totalRead += read;
                if (totalBytes > 0)
                {
                    progress?.Report((int)((totalRead * 100L) / totalBytes));
                }
            }
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = tempFile,
            Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
            UseShellExecute = true
        });
    }

    private class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("body")]
        public string Body { get; set; } = "";

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto>? Assets { get; set; }
    }

    private class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }
}
