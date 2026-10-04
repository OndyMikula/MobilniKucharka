using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using MobilniKucharka.Classes.Navigation;
using MobilniKucharka.Services;

namespace MobilniKucharka.Services.Feedback
{
    // Technické info o zařízení pro hlášení, bez unikátních ID
    public static class DeviceInfoCollector
    {
        public static string Collect()
        {
            var sb = new StringBuilder();

            Add(sb, "Zařízení", () => $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model} ({DeviceInfo.Current.DeviceType}, {DeviceInfo.Current.Idiom})");
            Add(sb, "OS", () => $"{DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}");
#if ANDROID
            Add(sb, "Android API", () => $"{(int)Android.OS.Build.VERSION.SdkInt}, security patch {Android.OS.Build.VERSION.SecurityPatch}");
            Add(sb, "Značka/kód", () => $"{Android.OS.Build.Brand} / {Android.OS.Build.Device} / {Android.OS.Build.Product}");
            Add(sb, "Čipset", () => $"{Android.OS.Build.Hardware} / {Android.OS.Build.Board}");
            Add(sb, "Firmware", () => $"{Android.OS.Build.Display} | {Android.OS.Build.Fingerprint}");
            Add(sb, "RAM", () =>
            {
                var am = (Android.App.ActivityManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.ActivityService);
                var mi = new Android.App.ActivityManager.MemoryInfo();
                am?.GetMemoryInfo(mi);
                return $"{mi.TotalMem / (1024 * 1024)} MB celkem, {mi.AvailMem / (1024 * 1024)} MB volných";
            });
            Add(sb, "WebView", () =>
            {
                if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return "n/a";
                var pkg = Android.Webkit.WebView.CurrentWebViewPackage;
                return $"{pkg?.PackageName} {pkg?.VersionName}";
            });
#endif
            Add(sb, "Displej", () =>
            {
                var d = DeviceDisplay.Current.MainDisplayInfo;
                return $"{d.Width:0}x{d.Height:0} px, density {d.Density:0.##}, {d.Orientation}, {d.RefreshRate:0} Hz";
            });
            Add(sb, "Insety (dp)", () => $"top {SystemInsets.TopDp:0.#}, bottom {SystemInsets.BottomDp:0.#}");
            Add(sb, "Motiv", () => $"systém {AppInfo.Current.RequestedTheme}, appka {Preferences.Default.Get("AppTheme", "Podle systému")}");
            Add(sb, "Lokalizace", () => CultureInfo.CurrentCulture.Name);
            Add(sb, "Síť", () => $"{Connectivity.Current.NetworkAccess}, {string.Join("/", Connectivity.Current.ConnectionProfiles)}");
            Add(sb, "Úspora energie", () => Battery.Default.EnergySaverStatus.ToString());
            Add(sb, "Appka", () => $"{AppInfo.Current.VersionString} (build {AppInfo.Current.BuildString})");
            Add(sb, ".NET", () => RuntimeInformation.FrameworkDescription);

            return sb.ToString().TrimEnd();
        }

        private static void Add(StringBuilder sb, string label, Func<string> getValue)
        {
            try { sb.AppendLine($"{label}: {getValue()}"); }
            catch { sb.AppendLine($"{label}: ?"); }
        }
    }
}