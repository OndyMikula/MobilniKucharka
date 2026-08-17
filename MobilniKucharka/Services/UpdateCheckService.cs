using System.Text.Json;
using System.Text.RegularExpressions;

namespace MobilniKucharka.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public string? ApkDownloadUrl { get; set; }
    }

    public partial class UpdateCheckService
    {
        private readonly HttpClient _httpClient = new();
        private const string RepoOwner = "OndyMikula";
        private const string RepoName = "MobilniKucharka";

        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            if (IsInstalledFromGooglePlay())
                return null;

            try
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MobilniKucharka-App");

                bool isBetaBuild = AppInfo.Current.VersionString.Contains("beta", StringComparison.OrdinalIgnoreCase);
                bool isOptedIntoBeta = Preferences.Default.Get("IsBetaOptedIn", false);
                bool checkBothChannels = isBetaBuild || isOptedIntoBeta;

                string url = checkBothChannels
                    ? $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases"
                    : $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var contentString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(contentString)) return null;

                JsonElement releaseElement;

                if (checkBothChannels)
                {
                    if (!contentString.TrimStart().StartsWith('[')) return null;
                    var releases = JsonSerializer.Deserialize<JsonElement>(contentString);
                    if (releases.ValueKind != JsonValueKind.Array || releases.GetArrayLength() == 0) return null;
                    releaseElement = releases[0];
                }
                else
                {
                    if (!contentString.TrimStart().StartsWith('{')) return null;
                    releaseElement = JsonSerializer.Deserialize<JsonElement>(contentString);
                }

                string tagName = releaseElement.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string htmlUrl = releaseElement.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";

                string? apkUrl = null;
                if (releaseElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        if (name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                        {
                            apkUrl = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                            break;
                        }
                    }
                }

                string latestVersion = tagName.TrimStart('v', 'V');
                string currentVersion = AppInfo.Current.VersionString;

                return new UpdateInfo
                {
                    IsUpdateAvailable = CompareVersions(latestVersion, currentVersion) > 0,
                    LatestVersion = latestVersion,
                    ReleaseUrl = htmlUrl,
                    ApkDownloadUrl = apkUrl
                };
            }
            catch
            {
                return null;
            }
        }

        private static bool IsInstalledFromGooglePlay()
        {
#if ANDROID
            try
            {
                if (!OperatingSystem.IsAndroidVersionAtLeast(30)) return false;

                var context = Android.App.Application.Context;
                string packageName = context.PackageName ?? "";
                var packageManager = context.PackageManager;

                if (packageManager == null) return false;

                string? installer = packageManager.GetInstallSourceInfo(packageName)?.InstallingPackageName;
                return installer == "com.android.vending";
            }
            catch
            {
                return false;
            }
#else
    return false;
#endif
        }

        [GeneratedRegex(@"^\d+(\.\d+)*")]
        private static partial Regex VersionCoreRegexGen();

        private static (string Core, bool IsBeta) SplitVersionAndBeta(string version)
        {
            bool isBeta = version.Contains("beta", StringComparison.OrdinalIgnoreCase);
            var match = VersionCoreRegexGen().Match(version);
            string core = match.Success ? match.Value : version;
            return (core, isBeta);
        }

        private static int CompareVersions(string v1, string v2)
        {
            var (core1, isBeta1) = SplitVersionAndBeta(v1);
            var (core2, isBeta2) = SplitVersionAndBeta(v2);

            var parts1 = core1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
            var parts2 = core2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

            int maxLength = Math.Max(parts1.Length, parts2.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int p1 = i < parts1.Length ? parts1[i] : 0;
                int p2 = i < parts2.Length ? parts2[i] : 0;
                if (p1 != p2) return p1.CompareTo(p2);
            }

            if (isBeta1 == isBeta2) return 0;
            return isBeta1 ? -1 : 1; // beta a stabilní se stejným číslem -> stabilní vyhrává
        }
    }
}