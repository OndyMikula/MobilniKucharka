using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

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
        // Android 15+ (API 35, cílová platforma tohohle projektu) vynucuje edge-to-edge layout
        // bez možnosti to vypnout - obsah appky (včetně BlazorWebView) se kreslí i pod systémovou
        // navigační lištou (gesta i klasická 3-tlačítková), appka si musí prostor pro ni rezervovat
        // sama. Statický event ať si BlazorShellPage (jediné místo, které BlazorWebView hostuje)
        // umí přihlásit odběr bez nutnosti formální DI - stejný vzor jako App.PendingImportGuid.
        public static event Action<double>? SystemBottomInsetChanged;

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
        // edge-to-edge), takže hlásí skutečnou výšku systémové navigační lišty v pixelech - liší
        // se podle gesta/3 tlačítka a je 0, pokud zařízení žádnou nerezervuje. Zůstává aktivní po
        // celou dobu běhu appky (ne jednorázové čtení při startu), takže se spacer v BlazorShellPage
        // přizpůsobí i za běhu - otočení obrazovky, přepnutí gesta/3 tlačítka v nastavení telefonu.
        private void SetupSystemInsetsListener()
        {
            var decorView = Window?.DecorView;
            if (decorView == null) return;

            ViewCompat.SetOnApplyWindowInsetsListener(decorView, new SystemBarsInsetsListener());
        }

        private class SystemBarsInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            // Signatura musí přesně odpovídat nullable anotacím skutečného rozhraní
            // IOnApplyWindowInsetsListener (v?, insets?, návratový typ WindowInsetsCompat?) -
            // jinak CS8767. "insets" může teoreticky přijít null (odtud i CS8602 dřív) -
            // v tom případě ho jen vrátíme beze změny, stejně jako by to udělala výchozí
            // implementace bez posluchače.
            public WindowInsetsCompat? OnApplyWindowInsets(global::Android.Views.View? v, WindowInsetsCompat? insets)
            {
                if (insets == null) return insets;

                // GetInsets je anotované jako vracející nullable Insets? (i když v praxi Android vždy vrátí
                // instanci, jen s nulovými hodnotami, pokud daný typ inset neexistuje) - ?. + ?? 0 pokrývá
                // oba případy bez zbytečného if-null bloku navíc.
                var navBarInsets = insets.GetInsets(WindowInsetsCompat.Type.NavigationBars());
                double density = v?.Resources?.DisplayMetrics?.Density ?? 1.0;
                double insetDp = (navBarInsets?.Bottom ?? 0) / density;

                SystemBottomInsetChanged?.Invoke(insetDp);

                return insets;
            }
        }
    }
}