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

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        string selectedLanguage = LanguagePicker.SelectedItem?.ToString() ?? "Čeština";

        // Uložíme název pro Picker
        Preferences.Default.Set("AppLanguageName", selectedLanguage);

        // Uložíme systémový kód pro CultureInfo ("en" nebo "cs")
        string cultureCode = selectedLanguage == "English" ? "en" : "cs";
        Preferences.Default.Set("AppLanguageCode", cultureCode);
    }
}