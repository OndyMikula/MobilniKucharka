using System.Globalization;

namespace MobilniKucharka;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        // Načtení uloženého motivu
        string savedTheme = Preferences.Default.Get("AppTheme", "Podle systému");
        ThemePicker.SelectedItem = savedTheme;

        // Načtení uloženého jazyka
        string savedLanguage = Preferences.Default.Get("AppLanguageName", "Čeština");
        LanguagePicker.SelectedItem = savedLanguage;
    }

    private void OnThemeChanged(object sender, EventArgs e)
    {
        string selectedTheme = ThemePicker.SelectedItem.ToString() ?? "Podle systému";
        Preferences.Default.Set("AppTheme", selectedTheme);

        // Okamžitá aplikace motivu
        if (selectedTheme == "Světlý (Light)")
            Application.Current!.UserAppTheme = AppTheme.Light;
        else if (selectedTheme == "Tmavý (Dark)")
            Application.Current!.UserAppTheme = AppTheme.Dark;
        else
            Application.Current!.UserAppTheme = AppTheme.Unspecified;
    }

    private async void OnLanguageChanged(object sender, EventArgs e)
    {
        if (LanguagePicker.SelectedItem == null) return;

        string selectedLanguage = LanguagePicker.SelectedItem.ToString()!;
        string currentSavedCode = Preferences.Default.Get("AppLanguageCode", "cs");
        string newCultureCode = selectedLanguage == "English" ? "en" : "cs";

        // Pokud uživatel klikl na stejný jazyk, nic nedělej
        if (currentSavedCode == newCultureCode) return;

        // Lokální překlad dialogu podle toho, jaký jazyk uživatel zrovna vybral
        string title = newCultureCode == "en" ? "Language Change" : "Změna jazyka";
        string message = newCultureCode == "en"
            ? "The app needs to reload to apply the language change. Do you want to reload now?"
            : "Pro uplatnění změn je potřeba aplikaci znovu načíst. Chcete ji restartovat nyní?";
        string accept = newCultureCode == "en" ? "Restart" : "Restartovat";
        string cancel = newCultureCode == "en" ? "Cancel" : "Zrušit";

        bool shouldRestart = await DisplayAlert(title, message, accept, cancel);

        if (shouldRestart)
        {
            // 1. Uložíme nové nastavení
            Preferences.Default.Set("AppLanguageName", selectedLanguage);
            Preferences.Default.Set("AppLanguageCode", newCultureCode);

            // 2. Nastavíme novou kulturu pro celou aplikaci
            var culture = new CultureInfo(newCultureCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // 3. Soft-restart: Znovu vygenerujeme celou navigační strukturu
            Application.Current!.Windows[0].Page = new NavigationPage(new MainPage());
        }
        else
        {
            // Pokud uživatel stornoval, vrátíme Picker na původní hodnotu
            LanguagePicker.SelectedItem = currentSavedCode == "en" ? "English" : "Čeština";
        }
    }
}