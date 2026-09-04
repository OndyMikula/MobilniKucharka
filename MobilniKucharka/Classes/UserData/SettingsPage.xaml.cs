using CommunityToolkit.Maui.Storage;
using MobilniKucharka.Classes.Legal;
using MobilniKucharka.Classes.Recipe.Sharing;
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

    private static string Tr(string csText) => MobilniKucharka.Translation.UiTranslator.Tr(csText);

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

            await DisplayAlertAsync(Tr("Vývojářský režim"),
                newState ? Tr("Vývojářský režim byl aktivován.") : Tr("Vývojářský režim byl deaktivován."), "OK");
        }
    }

    private static readonly string[] ThemeKeys = ["Podle systému", "Světlý", "Tmavý"];

    private void LoadCurrentSettings()
    {
        ThemePicker.ItemsSource = ThemeKeys.Select(Tr).ToList();

        string savedTheme = Preferences.Default.Get("AppTheme", "Podle systému");
        int themeIndex = Array.IndexOf(ThemeKeys, savedTheme);
        ThemePicker.SelectedIndex = themeIndex >= 0 ? themeIndex : 0;

        string savedLanguage = Preferences.Default.Get("AppLanguageName", "Čeština");
        LanguagePicker.SelectedItem = savedLanguage;

        AppVersionLabel.Text = string.Format(Tr("Verze {0}"), AppInfo.Current.VersionString);
    }

    private void OnThemeChanged(object sender, EventArgs e)
    {
        int index = ThemePicker.SelectedIndex;
        string selectedTheme = index >= 0 && index < ThemeKeys.Length ? ThemeKeys[index] : "Podle systému";
        Preferences.Default.Set("AppTheme", selectedTheme);

        if (selectedTheme == "Světlý")
            Application.Current!.UserAppTheme = AppTheme.Light;
        else if (selectedTheme == "Tmavý")
            Application.Current!.UserAppTheme = AppTheme.Dark;
        else
            Application.Current!.UserAppTheme = AppTheme.Unspecified;
    }

    // POZOR: tenhle dialog je záměrně NEpřekládaný přes Tr() - ptá se, do jakého jazyka appku přepnout,
    // takže musí být napůl v obou jazycích ještě PŘED přepnutím, ne v aktuálním (starém) jazyce.
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

        bool shouldRestart = await DisplayAlertAsync(title, message, accept, cancel);

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
        BackupProgressLabel.Text = Tr("Exportuji data...");

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
                Title = Tr("Uložit zálohu Mobilní Kuchařky"),
                File = new ShareFile(zipPath)
            });
        }
        catch (Exception ex)
        {
            BackupProgressOverlay.IsVisible = false;
            await DisplayAlertAsync(Tr("Chyba"), $"{Tr("Export se nepodařil")}: {ex.Message}", "OK");
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
                PickerTitle = Tr("Vyber soubor zálohy (.zip)"),
                FileTypes = ZipFileType
            });
            if (result == null) return;

            bool confirm = await DisplayAlertAsync(Tr("Načíst zálohu"), Tr("Tímto se přepíší všechna aktuální data v aplikaci. Pokračovat?"), Tr("Ano"), Tr("Zrušit"));
            if (!confirm) return;

            BackupProgressOverlay.IsVisible = true;
            BackupProgressLabel.Text = Tr("Načítám data...");

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
            await DisplayAlertAsync(Tr("Hotovo"), Tr("Data byla načtena. Aplikace se nyní restartuje."), Tr("Restartovat"));
            RestartApp();
        }
        catch (Exception ex)
        {
            BackupProgressOverlay.IsVisible = false;
            await DisplayAlertAsync(Tr("Chyba"), $"{Tr("Načtení se nepodařilo")}: {ex.Message}", "OK");
        }
    }

    private async void OnSaveDataToDeviceClicked(object sender, EventArgs e)
    {
        BackupProgressOverlay.IsVisible = true;
        BackupProgressLabel.Text = Tr("Exportuji data...");

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
                await DisplayAlertAsync(Tr("Hotovo"), $"{Tr("Záloha byla uložena do")}: {result.FilePath}", "OK");
        }
        catch (Exception ex)
        {
            BackupProgressOverlay.IsVisible = false;
            await DisplayAlertAsync(Tr("Chyba"), $"{Tr("Uložení se nepodařilo")}: {ex.Message}", "OK");
        }
    }

    private async void OnImportRecipeClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = Tr("Vyber soubor receptu") });
            if (result == null) return;

            var recipe = await RecipeShareService.ImportRecipeAsync(result.FullPath);

            int newId = await App.Database.ImportSharedRecipeAsync(recipe);
            await App.Database.AddRecipeToCategoryAsync(newId, "Vytvořené recepty");

            await DisplayAlertAsync(Tr("Hotovo"), string.Format(Tr("Recept \"{0}\" byl naimportován."), recipe.Name), "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(Tr("Chyba"), $"{Tr("Import se nepodařil")}: {ex.Message}", "OK");
        }
    }

    private async void OnImportFromLinkClicked(object sender, EventArgs e)
    {
        string? link = RecipeLinkEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(link))
        {
            await DisplayAlertAsync(Tr("Chyba"), Tr("Nejdřív vlož odkaz."), "OK");
            return;
        }

        string? guid = ExtractGuidFromLink(link);
        if (guid == null)
        {
            await DisplayAlertAsync(Tr("Chyba"), Tr("Tento odkaz nevypadá jako platný odkaz na recept."), "OK");
            return;
        }

        var recipe = await RecipeLinkShareService.ImportFromLinkAsync(guid);
        if (recipe == null)
        {
            await DisplayAlertAsync(Tr("Odkaz neplatný"), Tr("Tento odkaz na recept už není platný (buď vypršel, nebo už byl použit)."), "OK");
            return;
        }

        int newId = await App.Database.ImportSharedRecipeAsync(recipe);
        await App.Database.AddRecipeToCategoryAsync(newId, "Vytvořené recepty");

        RecipeLinkEntry.Text = "";
        await DisplayAlertAsync(Tr("Hotovo"), string.Format(Tr("Recept \"{0}\" byl naimportován."), recipe.Name), "OK");
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
            await DisplayAlertAsync(Tr("Kontrola aktualizací"), Tr("Nepodařilo se zkontrolovat aktualizace. Zkontroluj internetové připojení."), "OK");
            return;
        }

        if (!info.IsUpdateAvailable)
        {
            await DisplayAlertAsync(Tr("Kontrola aktualizací"), Tr("Používáš nejnovější verzi."), "OK");
            return;
        }

        bool download = await DisplayAlertAsync(
            Tr("Nová verze je k dispozici"),
            string.Format(Tr("Je dostupná nová verze aplikace ({0}). Chceš ji instalovat? Všechny recepty, záložky a nastavení zůstanou zachovány."), info.LatestVersion),
            Tr("Instalovat"), Tr("Pokračovat bez instalace"));

        if (download)
        {
            string urlToOpen = !string.IsNullOrWhiteSpace(info.ApkDownloadUrl) ? info.ApkDownloadUrl : info.ReleaseUrl;
            if (!string.IsNullOrWhiteSpace(urlToOpen))
                await Launcher.Default.OpenAsync(urlToOpen);
        }
    }

    // Jeden odkaz "Právní informace" místo čtyř samostatných tlačítek - klepnutím nabídne action
    // sheet se všemi 4 dokumenty. License jde do vlastní LicensePage (plné znění Apache 2.0),
    // zbylé tři přes generickou LegalDocumentPage (viz LegalContent).
    private async void OnLegalInfoTapped(object sender, TappedEventArgs e)
    {
        string tos = Tr("Podmínky použití");
        string privacy = Tr("Zásady ochrany osobních údajů");
        string license = Tr("Licence");
        string thirdParty = Tr("Zdroje a licence třetích stran");

        string action = await DisplayActionSheetAsync(Tr("Právní informace"), Tr("Zrušit"), null, tos, privacy, license, thirdParty);

        if (action == tos)
            await Navigation.PushAsync(new LegalDocumentPage(LegalDocumentType.TermsOfService));
        else if (action == privacy)
            await Navigation.PushAsync(new LegalDocumentPage(LegalDocumentType.PrivacyPolicy));
        else if (action == license)
            await Navigation.PushAsync(new LicensePage());
        else if (action == thirdParty)
            await Navigation.PushAsync(new LegalDocumentPage(LegalDocumentType.ThirdPartyNotices));
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
                ? "🧪 " + Tr("Právě běžíš na beta verzi aplikace.")
                : "✅ " + Tr("Jsi připojen jako beta tester. Dostáváš i beta verze aplikace.");
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
            await DisplayAlertAsync(Tr("Hotovo"), Tr("Jsi přihlášen jako beta tester. Nyní budeš dostávat i beta verze."), "OK");
        }
        else
        {
            await DisplayAlertAsync(Tr("Špatný kód"), Tr("Zadaný kód není správný."), "OK");
        }
    }

    private async void OnUnregisterBetaClicked(object sender, EventArgs e)
    {
        Preferences.Default.Set("IsBetaOptedIn", false);
        UpdateBetaSectionVisibility();
        await DisplayAlertAsync(Tr("Hotovo"), Tr("Byl jsi odhlášen z beta programu."), "OK");
    }

    // Nahlásit chybu je Blazor route (/bug-report) - SettingsPage je zatím pořád nativní (Fáze 3
    // zatím nedokončená), takže se používá stejný most jako dřív u BookmarkCategoryPage
    // (App.PendingBlazorRoute + PopToRootAsync na BlazorShellPage, viz MainLayout.razor).
    private async void OnReportBugClicked(object sender, EventArgs e)
    {
        App.PendingBlazorRoute = "/bug-report";
        await Navigation.PopToRootAsync();
    }

    private static void RestartApp()
    {
        App.ResetDatabase();
        Application.Current!.Windows[0].Page = new AppShell();
    }
}