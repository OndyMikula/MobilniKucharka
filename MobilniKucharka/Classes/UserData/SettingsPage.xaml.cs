using CommunityToolkit.Maui.Storage;
using MobilniKucharka.Services;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MobilniKucharka;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        LoadCurrentSettings();
        UpdateBetaSectionVisibility();
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
            UpdateBetaSectionVisibility();

            await DisplayAlert("Vývojářský režim",
                newState ? "Vývojářský režim byl aktivován." : "Vývojářský režim byl deaktivován.", "OK");
        }
    }

    private void LoadCurrentSettings()
    {
        string savedTheme = Preferences.Default.Get("AppTheme", "Podle systému");
        ThemePicker.SelectedItem = savedTheme;

        string savedLanguage = Preferences.Default.Get("AppLanguageName", "Čeština");
        LanguagePicker.SelectedItem = savedLanguage;

        AppVersionLabel.Text = $"Verze {AppInfo.Current.VersionString}";
    }

    private void OnThemeChanged(object sender, EventArgs e)
    {
        string selectedTheme = ThemePicker.SelectedItem.ToString() ?? "Podle systému";
        Preferences.Default.Set("AppTheme", selectedTheme);

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

        if (currentSavedCode == newCultureCode) return;

        string title = newCultureCode == "en" ? "Language Change" : "Změna jazyka";
        string message = newCultureCode == "en"
            ? "The app needs to reload to apply the language change. Do you want to reload now?"
            : "Pro uplatnění změn je potřeba aplikaci znovu načíst. Chcete ji restartovat nyní?";
        string accept = newCultureCode == "en" ? "Restart" : "Restartovat";
        string cancel = newCultureCode == "en" ? "Cancel" : "Zrušit";

        bool shouldRestart = await DisplayAlert(title, message, accept, cancel);

        if (shouldRestart)
        {
            Preferences.Default.Set("AppLanguageName", selectedLanguage);
            Preferences.Default.Set("AppLanguageCode", newCultureCode);

            var culture = new CultureInfo(newCultureCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            RestartApp();
        }
        else
        {
            LanguagePicker.SelectedItem = currentSavedCode == "en" ? "English" : "Čeština";
        }
    }

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
            string zipPath = await DataBackupService.ExportAsync(progress);
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

    private static readonly FilePickerFileType ZipFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, new[] { "application/zip", "application/x-zip-compressed" } }
    });

    private async void OnLoadDataClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Vyber soubor zálohy (.zip)",
                FileTypes = ZipFileType
            });
            if (result == null) return;

            bool confirm = await DisplayAlert("Načíst zálohu", "Tímto se přepíší všechna aktuální data v aplikaci. Pokračovat?", "Ano", "Zrušit");
            if (!confirm) return;

            BackupProgressOverlay.IsVisible = true;
            BackupProgressLabel.Text = "Načítám data...";

            string localCopyPath = Path.Combine(FileSystem.CacheDirectory, $"import_{Guid.NewGuid()}.zip");
            using (var sourceStream = await result.OpenReadAsync())
            using (var localStream = File.Create(localCopyPath))
            {
                await sourceStream.CopyToAsync(localStream);
            }

            var progress = new Progress<double>(value =>
            {
                BackupProgressBar.Progress = value;
                BackupProgressPercentLabel.Text = $"{value:P0}";
            });

            await DataBackupService.ImportAsync(localCopyPath, progress);

            File.Delete(localCopyPath);

            BackupProgressOverlay.IsVisible = false;
            await DisplayAlert("Hotovo", "Data byla načtena. Aplikace se nyní restartuje.", "Restartovat");
            RestartApp();
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
            string zipPath = await DataBackupService.ExportAsync(progress);
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

    private async void OnImportRecipeClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Vyber soubor receptu" });
            if (result == null) return;

            var recipe = await RecipeShareService.ImportRecipeAsync(result.FullPath);

            int newId = await App.Database.ImportSharedRecipeAsync(recipe);
            await App.Database.AddRecipeToCategoryAsync(newId, "Vytvořené recepty");

            await DisplayAlert("Hotovo", $"Recept \"{recipe.Name_CS}\" byl naimportován.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Chyba", $"Import se nepodařil: {ex.Message}", "OK");
        }
    }

    private async void OnImportFromLinkClicked(object sender, EventArgs e)
    {
        string? link = RecipeLinkEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(link))
        {
            await DisplayAlert("Chyba", "Nejdřív vlož odkaz.", "OK");
            return;
        }

        string? guid = ExtractGuidFromLink(link);
        if (guid == null)
        {
            await DisplayAlert("Chyba", "Tento odkaz nevypadá jako platný odkaz na recept.", "OK");
            return;
        }

        var recipe = await RecipeLinkShareService.ImportFromLinkAsync(guid);
        if (recipe == null)
        {
            await DisplayAlert("Odkaz neplatný", "Tento odkaz na recept už není platný (buď vypršel, nebo už byl použit).", "OK");
            return;
        }

        int newId = await App.Database.ImportSharedRecipeAsync(recipe);
        await App.Database.AddRecipeToCategoryAsync(newId, "Vytvořené recepty");

        RecipeLinkEntry.Text = "";
        await DisplayAlert("Hotovo", $"Recept \"{recipe.Name_CS}\" byl naimportován.", "OK");
    }

    [GeneratedRegex(@"[?&]id=([a-fA-F0-9]+)")]
    private static partial Regex QueryIdRegexGen();

    [GeneratedRegex(@"recipe/([a-fA-F0-9]+)")]
    private static partial Regex PathIdRegexGen();

    private static string? ExtractGuidFromLink(string link)
    {
        var queryMatch = QueryIdRegexGen().Match(link);
        if (queryMatch.Success) return queryMatch.Groups[1].Value;

        var pathMatch = PathIdRegexGen().Match(link);
        if (pathMatch.Success) return pathMatch.Groups[1].Value;

        return null;
    }

    private async void OnCheckForUpdatesClicked(object sender, EventArgs e)
    {
        var updateService = new UpdateCheckService();
        var info = await updateService.CheckForUpdateAsync();

        if (info == null)
        {
            await DisplayAlert("Kontrola aktualizací", "Nepodařilo se zkontrolovat aktualizace. Zkontroluj internetové připojení.", "OK");
            return;
        }

        if (!info.IsUpdateAvailable)
        {
            await DisplayAlert("Kontrola aktualizací", "Používáš nejnovější verzi.", "OK");
            return;
        }

        bool download = await DisplayAlert(
            "Nová verze je k dispozici",
            $"Je dostupná nová verze aplikace ({info.LatestVersion}). Chceš ji instalovat? Všechny recepty, záložky a nastavení zůstanou zachovány.",
            "Instalovat", "Pokračovat bez instalace");

        if (download)
        {
            string urlToOpen = !string.IsNullOrWhiteSpace(info.ApkDownloadUrl) ? info.ApkDownloadUrl : info.ReleaseUrl;
            if (!string.IsNullOrWhiteSpace(urlToOpen))
                await Launcher.Default.OpenAsync(urlToOpen);
        }
    }

    private void UpdateBetaSectionVisibility()
    {
        bool isDevMode = Preferences.Default.Get("IsDeveloperMode", false);

        if (!isDevMode)
        {
            BetaOptedInSection.IsVisible = false;
            BetaOptInSection.IsVisible = false;
            return;
        }

        bool isBetaBuild = AppInfo.Current.VersionString.Contains("beta", StringComparison.OrdinalIgnoreCase);
        bool isOptedIn = Preferences.Default.Get("IsBetaOptedIn", false);
        bool effectivelyOptedIn = isBetaBuild || isOptedIn;

        BetaOptedInSection.IsVisible = effectivelyOptedIn;
        BetaOptInSection.IsVisible = !effectivelyOptedIn;

        if (effectivelyOptedIn)
        {
            BetaOptedInStatusLabel.Text = isBetaBuild
                ? "🧪 Právě běžíš na beta verzi aplikace."
                : "✅ Jsi připojen jako beta tester. Dostáváš i beta verze aplikace.";
            UnregisterBetaButton.IsVisible = !isBetaBuild;
        }
    }

    private async void OnRegisterBetaClicked(object sender, EventArgs e)
    {
        if (string.Equals(BetaCodeEntry.Text?.Trim(), "beta", StringComparison.OrdinalIgnoreCase))
        {
            Preferences.Default.Set("IsBetaOptedIn", true);
            BetaCodeEntry.Text = "";
            UpdateBetaSectionVisibility();
            await DisplayAlert("Hotovo", "Jsi zaregistrovaný jako beta tester. Nyní budeš dostávat i beta verze.", "OK");
        }
        else
        {
            await DisplayAlert("Špatný kód", "Zadaný kód není správný.", "OK");
        }
    }

    private async void OnUnregisterBetaClicked(object sender, EventArgs e)
    {
        Preferences.Default.Set("IsBetaOptedIn", false);
        UpdateBetaSectionVisibility();
        await DisplayAlert("Hotovo", "Byl jsi odregistrován z beta programu.", "OK");
    }

    private static void RestartApp()
    {
        App.ResetDatabase();
        Application.Current!.Windows[0].Page = new NavigationPage(new MainPage());
    }
}