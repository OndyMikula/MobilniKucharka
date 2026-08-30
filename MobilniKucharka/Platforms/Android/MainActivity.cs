using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using MobilniKucharka.Classes.Navigation;

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
            SetupSystemInsetsListener();
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

        // Naslouchač visí na DecorView (ten vždy pokrývá celou obrazovku bez ohledu na
        // edge-to-edge), takže hlásí skutečnou výšku systémové navigační lišty v pixelech. Hodnota
        // jde přímo do platformově neutrálního SystemInsets (viz Classes/Navigation/SystemInsets.cs)
        // - Blazor komponenty tak nemusí odkazovat na Android-specifický kód. Zůstává aktivní po
        // celou dobu běhu appky, takže se appčin CSS nav bar přizpůsobí i za běhu (otočení
        // obrazovky, přepnutí gesta/3 tlačítka v nastavení telefonu).
        private void SetupSystemInsetsListener()
        {
            var decorView = Window?.DecorView;
            if (decorView == null) return;

            ViewCompat.SetOnApplyWindowInsetsListener(decorView, new SystemBarsInsetsListener());
        }

        private class SystemBarsInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            public WindowInsetsCompat? OnApplyWindowInsets(global::Android.Views.View? v, WindowInsetsCompat? insets)
            {
                if (insets == null) return insets;

                var navBarInsets = insets.GetInsets(WindowInsetsCompat.Type.NavigationBars());
                double density = v?.Resources?.DisplayMetrics?.Density ?? 1.0;
                double insetDp = (navBarInsets?.Bottom ?? 0) / density;

                SystemInsets.SetBottom(insetDp);

                return insets;
            }
        }
    }
}