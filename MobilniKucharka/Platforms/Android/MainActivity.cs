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
        private int _rawTopPx, _rawBottomPx;
        private global::Android.Webkit.WebView? _webView;

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

        private void SetupSystemInsetsListener()
        {
            var decorView = Window?.DecorView;
            if (decorView == null) return;

            ViewCompat.SetOnApplyWindowInsetsListener(decorView, new SystemBarsInsetsListener(OnRawInsets));

            var observer = decorView.ViewTreeObserver;
            observer?.GlobalLayout += (_, _) => ReportInsets();
        }

        private void OnRawInsets(int topPx, int bottomPx)
        {
            _rawTopPx = topPx;
            _rawBottomPx = bottomPx;
            Window?.DecorView?.Post(ReportInsets);
        }

        // Nahlásí jen skutečný překryv WebView se system bary (souřadnice obrazovky)
        private void ReportInsets()
        {
            var decor = Window?.DecorView;
            if (decor == null) return;

            if (_webView == null || !_webView.IsShown)
                _webView = FindVisibleWebView(decor);

            int topPx = 0, bottomPx = 0;
            if (_webView != null)
            {
                var loc = new int[2];
                _webView.GetLocationOnScreen(loc);

                int screenHeight = GetRealScreenHeight();
                topPx = Math.Max(0, _rawTopPx - loc[1]);
                bottomPx = screenHeight > 0
                    ? Math.Max(0, loc[1] + _webView.Height - (screenHeight - _rawBottomPx))
                    : 0;
            }

            double density = decor.Resources?.DisplayMetrics?.Density ?? 1.0;
            SystemInsets.SetBottom(bottomPx / density);
            SystemInsets.SetTop(topPx / density);
        }

        private int GetRealScreenHeight()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30) && WindowManager?.MaximumWindowMetrics?.Bounds is { } bounds)
                return bounds.Height();

            var metrics = new global::Android.Util.DisplayMetrics();
#pragma warning disable CA1422
            WindowManager?.DefaultDisplay?.GetRealMetrics(metrics);
#pragma warning restore CA1422
            return metrics.HeightPixels;
        }

        private static global::Android.Webkit.WebView? FindVisibleWebView(global::Android.Views.View? view)
        {
            if (view is global::Android.Webkit.WebView web && web.IsShown) return web;
            if (view is ViewGroup group)
            {
                for (int i = 0; i < group.ChildCount; i++)
                {
                    var found = FindVisibleWebView(group.GetChildAt(i));
                    if (found != null) return found;
                }
            }
            return null;
        }

        private class SystemBarsInsetsListener(Action<int, int> onInsets) : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            public WindowInsetsCompat? OnApplyWindowInsets(global::Android.Views.View? v, WindowInsetsCompat? insets)
            {
                if (insets == null) return insets;

                var statusBars = insets.GetInsets(WindowInsetsCompat.Type.StatusBars());
                var navBars = insets.GetInsets(WindowInsetsCompat.Type.NavigationBars());
                onInsets(statusBars?.Top ?? 0, navBars?.Bottom ?? 0);

                return insets;
            }
        }
    }
}