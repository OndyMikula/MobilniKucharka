#if ANDROID
using MobilniKucharka.Platforms.Android;
#endif

namespace MobilniKucharka.Classes.Navigation
{
    public partial class BlazorShellPage : ContentPage
    {
        public static event Action? ShellAppeared;

        public BlazorShellPage()
        {
            InitializeComponent();

#if ANDROID
            // #if ANDROID i přesto, že projekt aktuálně cílí jen net10.0-android (viz CLAUDE.md) -
            // zbylé Platforms/ složky (iOS, Windows...) jsou v repu jako šablonový pozůstatek, tenhle
            // guard je levná pojistka, kdyby se multi-target někdy znovu zapnul.
            MainActivity.SystemBottomInsetChanged += OnSystemBottomInsetChanged;
#endif
        }

#if ANDROID
        private void OnSystemBottomInsetChanged(double insetDp)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SystemNavInsetSpacer.HeightRequest = insetDp;
            });
        }
#endif

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ShellAppeared?.Invoke();
        }
    }
}