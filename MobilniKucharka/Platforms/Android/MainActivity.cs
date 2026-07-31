using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace MobilniKucharka.Platforms.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter([Intent.ActionView],
        Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
        DataScheme = "https",
        DataHost = "ondymikula.github.io",
        DataPathPrefix = "/recipe.html",
        AutoVerify = true)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnResume()
        {
            base.OnResume();
            Window?.ClearFlags(WindowManagerFlags.DimBehind);
            Window?.SetDimAmount(0f);
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            HandleIntent(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleIntent(intent);
        }

        private static void HandleIntent(Intent? intent)
        {
            var uri = intent?.Data;
            if (uri != null && uri.Scheme == "https" && uri.Host == "ondymikula.github.io")
            {
                string? guid = uri.GetQueryParameter("id");
                if (!string.IsNullOrWhiteSpace(guid))
                    App.PendingImportGuid = guid;
            }
        }
    }
}