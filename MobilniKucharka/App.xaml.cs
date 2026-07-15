using System.Globalization;

namespace MobilniKucharka
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // 1. Načtení uloženého kódu jazyka (pokud není, použijeme "cs" pro češtinu)
            string savedCultureCode = Preferences.Default.Get("AppLanguageCode", "cs");

            // 2. Aplikování jazyka na hlavní vlákna aplikace
            var culture = new CultureInfo(savedCultureCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            MainPage = new NavigationPage(new MainPage());
        }
    }
}