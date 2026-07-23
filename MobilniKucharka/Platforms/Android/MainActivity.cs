using Android.App;
using Android.Content.PM;
using Android.Views;

namespace MobilniKucharka.Platforms.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnResume()
        {
            base.OnResume();

            // Po zavření DisplayAlert/DisplayActionSheet občas zůstane okno "ztmavené",
            // protože se systémový dim overlay správně neuklidí. Vynutíme čisté okno
            // při každém návratu aplikace do popředí.
            Window?.ClearFlags(WindowManagerFlags.DimBehind);
            Window?.SetDimAmount(0f);
        }
    }
}