using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace MobilniKucharka
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Blazor Hybrid - zatím jen zkušebně pro porovnání BottomNavBar (viz /Components).
            // Zbytek appky zůstává čistě XAML/MAUI, tohle nijak neovlivňuje.
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Logging.AddDebug();
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif

            return builder.Build();
        }
    }
}