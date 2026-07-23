using CommunityToolkit.Maui.Storage;
using MobilniKucharka.Services;
using System.Globalization;

namespace MobilniKucharka;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        LoadCurrentSettings();
    }

    private int _devModeTapCount = 0;
    private DateTime _lastDevModeTapTime = DateTime.MinValue;

    private async void OnDeveloperToggleClicked(object sender, EventArgs e)
    {
        var now = DateTime.Now;
        if ((now - _lastDevModeTapTime).TotalSeconds > 2)
            _devModeTapCount = 0;

        _lastDevModeTapTime = now;
        _devModeTapCount++;

        if (_devModeTapCount >= 3)
        {
            _devModeTapCount = 0;

            bool newState = !Preferences.Default.Get("IsDeveloperMode", false);
            Preferences.Default.Set("IsDeveloperMode", newState);

            await DisplayAlert("Vývojářský režim",
                newState ? "Vývojářský režim byl aktivován." : "Vývojářský režim byl deaktivován.", "OK");
        }
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

    private readonly DataBackupService _backupService = new();

    private async void OnExportDataClicked(object sender, EventArgs e)
    {
        BackupProgressOverlay.IsVisible = true;
        BackupProgressLabel.Text = "Exportuji data...";

        var progress = new Progress<double>(value =>
        {
            BackupProgressBar.Progress = value;
            BackupProgressPercentLabel.Text = $"{value:P0}";
        });

        try
        {
            string zipPath = await _backupService.ExportAsync(progress);
            BackupProgressOverlay.IsVisible = false;

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Uložit zálohu Mobilní Kuchařky",
                File = new ShareFile(zipPath)
            });
        }
        catch (Exception ex)
        {
            BackupProgressOverlay.IsVisible = false;
            await DisplayAlert("Chyba", $"Export se nepodařil: {ex.Message}", "OK");
        }
    }

    private async void OnLoadDataClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Vyber soubor zálohy (.zip)" });
            if (result == null) return;

            bool confirm = await DisplayAlert("Načíst zálohu", "Tímto se přepíší všechna aktuální data v aplikaci. Pokračovat?", "Ano", "Zrušit");
            if (!confirm) return;

            BackupProgressOverlay.IsVisible = true;
            BackupProgressLabel.Text = "Načítám data...";

            var progress = new Progress<double>(value =>
            {
                BackupProgressBar.Progress = value;
                BackupProgressPercentLabel.Text = $"{value:P0}";
            });

            await _backupService.ImportAsync(result.FullPath, progress);

            BackupProgressOverlay.IsVisible = false;
            await DisplayAlert("Hotovo", "Data byla načtena. Aplikace se nyní zavře — otevři ji prosím znovu ručně.", "OK");
            Application.Current?.Quit();
        }
        catch (Exception ex)
        {
            BackupProgressOverlay.IsVisible = false;
            await DisplayAlert("Chyba", $"Načtení se nepodařilo: {ex.Message}", "OK");
        }
    }

    private async void OnSaveDataToDeviceClicked(object sender, EventArgs e)
    {
        BackupProgressOverlay.IsVisible = true;
        BackupProgressLabel.Text = "Exportuji data...";

        var progress = new Progress<double>(value =>
        {
            BackupProgressBar.Progress = value;
            BackupProgressPercentLabel.Text = $"{value:P0}";
        });

        try
        {
            string zipPath = await _backupService.ExportAsync(progress);
            BackupProgressOverlay.IsVisible = false;

            using var stream = File.OpenRead(zipPath);
            var result = await FileSaver.Default.SaveAsync(Path.GetFileName(zipPath), stream, CancellationToken.None);

            if (result.IsSuccessful)
                await DisplayAlert("Hotovo", $"Záloha byla uložena do: {result.FilePath}", "OK");
        }
        catch (Exception ex)
        {
            BackupProgressOverlay.IsVisible = false;
            await DisplayAlert("Chyba", $"Uložení se nepodařilo: {ex.Message}", "OK");
        }
    }
}