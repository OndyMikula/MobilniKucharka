using System.Text.Json;

namespace MobilniKucharka.Services
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseUrl { get; set; } = string.Empty;
        public string? ApkDownloadUrl { get; set; }
    }

    public class UpdateCheckService
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

                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var contentString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(contentString) || !contentString.StartsWith('{'))
                    return null;

                var root = JsonSerializer.Deserialize<JsonElement>(contentString);

                string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "" : "";

                string? apkUrl = null;
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
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
                return null; // bez internetu / GitHub nedostupný -> potichu nic nedělat
            }
        }

        private static bool IsInstalledFromGooglePlay()
        {
#if ANDROID
            try
            {
                var context = Android.App.Application.Context;
                var packageManager = context.PackageManager;
                string packageName = context.PackageName ?? "";

                if (packageManager == null) return false;

                string? installer;

                if (OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    installer = packageManager.GetInstallSourceInfo(packageName)?.InstallingPackageName;
                }
                else
                {
                    installer = packageManager.GetInstallerPackageName(packageName); //cesta pro android verze 20+
                }

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

        private static int CompareVersions(string v1, string v2)
        {
            var parts1 = v1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
            var parts2 = v2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

            int maxLength = Math.Max(parts1.Length, parts2.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int p1 = i < parts1.Length ? parts1[i] : 0;
                int p2 = i < parts2.Length ? parts2[i] : 0;
                if (p1 != p2) return p1.CompareTo(p2);
            }
            return 0;
        }
    }
}