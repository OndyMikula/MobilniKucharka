#nullable disable
using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Classes.Recipe.Sharing;
using MobilniKucharka.Classes.UserData;
using MobilniKucharka.Classes.UserData.Bookmark;
using MobilniKucharka.Services;
using MobilniKucharka.Services.Api;

namespace MobilniKucharka
{
    public partial class MainPage : ContentPage
    {
        private readonly BudgetPlannerService _budgetService;

        public MainPage()
        {
            InitializeComponent();
            _budgetService = App.Database;
        }

        private static string Tr(string csText) => MobilniKucharka.Translation.UiTranslator.Tr(csText);

        // Zavolá se pokaždé, když se stránka zobrazí na displeji
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadRecipesDataAsync();
            UpdateSummaryUI();
            ResetDatabaseButton.IsVisible = Preferences.Default.Get("IsDeveloperMode", false);
            RepairStepsButton.IsVisible = Preferences.Default.Get("IsDeveloperMode", false);
            _ = CheckForUpdatesAsync();

            // Levný "nudge" pro překreslení - žádné čekání, žádný přepočet layoutu.
            Opacity = 0.999;
            Opacity = 1;

            if (!string.IsNullOrWhiteSpace(App.PendingImportGuid)) //link-sharing
            {
                string guid = App.PendingImportGuid;
                App.PendingImportGuid = null;
                _ = HandlePendingImportAsync(guid);
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            var updateService = new UpdateCheckService();
            var info = await updateService.CheckForUpdateAsync();

            if (info != null && info.IsUpdateAvailable)
            {
                bool download = await DisplayAlertAsync(
                    Tr("Nová verze je k dispozici"),
                    string.Format(Tr("Je dostupná nová verze aplikace ({0}). Chceš ji nainstalovat? Všechny recepty, záložky a nastavení zůstanou zachovány."), info.LatestVersion),
                    Tr("Instalovat"),
                    Tr("Pokračovat bez instalace"));

                if (download)
                {
                    string urlToOpen = !string.IsNullOrWhiteSpace(info.ApkDownloadUrl) ? info.ApkDownloadUrl : info.ReleaseUrl;
                    if (!string.IsNullOrWhiteSpace(urlToOpen))
                        await Launcher.Default.OpenAsync(urlToOpen);
                }
            }
        }

        // Tlačítko pro vytvoření nového receptu
        private async void OnCreateRecipeClicked(object sender, EventArgs e)
        {
            // Navigace na novou stránku pro tvorbu receptu
            await Navigation.PushAsync(new CreateRecipePage());
        }

        // Tlačítko pro vyhledávání receptů na základě nastavení
        private async void OnSearchRecipesClicked(object sender, EventArgs e)
        {
            var userDiets = ParseUserDiets();

            var mealDbService = new TheMealDbService();
            var found = await mealDbService.GetRandomRecipeMatchingDietAsync(userDiets);

            if (found == null)
            {
                await DisplayAlertAsync(Tr("Chyba"), Tr("Nepodařilo se najít žádný recept. Zkontroluj internetové připojení."), "OK");
                return;
            }

            var savedRecipe = await _budgetService.SaveExternalRecipeAsync(found);

            if (savedRecipe.ServingSize <= 0)
            {
                string result = await DisplayPromptAsync(
                    Tr("Pro kolik lidí by měl recept být?"),
                    Tr("Zadej, pro kolik lidí chceš recept uvařit."),
                    "OK", initialValue: string.Empty, keyboard: Keyboard.Numeric);

                if (int.TryParse(result, out var parsed) && parsed > 0)
                {
                    await _budgetService.UpdateRecipeServingSizeAsync(savedRecipe.Id, parsed);
                    savedRecipe.ServingSize = parsed;
                }
            }

            var recipeWithCost = new RecipeWithCost
            {
                Recipe = savedRecipe,
                CalculatedCost = 0, // MealDB neposkytuje ceny surovin, nemáme podle čeho počítat
                IsWithinBudget = true
            };

            await Navigation.PushAsync(new RecipeDetailPage(recipeWithCost));
        }

        private static List<string> ParseUserDiets()
        {
            string raw = Preferences.Default.Get("UserDiets", "");
            return string.IsNullOrWhiteSpace(raw)
                ? []
                : [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }

        private List<int> _lastLoadedRecipeIds = [];

        private async Task LoadRecipesDataAsync()
        {
            var recipes = await _budgetService.GetPlanAsync();

            var newIds = recipes.Select(r => r.Recipe.Id).ToList();
            bool isUnchanged = newIds.SequenceEqual(_lastLoadedRecipeIds);

            if (!isUnchanged)
            {
                RecipesCollectionView.ItemsSource = recipes;
                _lastLoadedRecipeIds = newIds;
            }
        }

        private void UpdateSummaryUI()
        {
            // Načtení aktuálních preferencí pro hlavičku
            int people = Preferences.Default.Get("PeopleCount", 2);
            double budget = Preferences.Default.Get("WeeklyBudget", 2000.0);

            string template = Tr("{0} osoby | Rozpočet: {1} Kč/týden");
            SettingsSummaryLabel.Text = string.Format(template, people, budget);
        }

        private async void OnEditSettingsClicked(object sender, EventArgs e)
        {
            // Otevře znovu OnboardingPage pro úpravu preferencí
            await Navigation.PushAsync(new OnboardingPage());
        }

        private async void OnRecipeTapped(object sender, TappedEventArgs e)
        {
            // ZMĚNA ZDE: Kontrolujeme, zda je sender Border, nikoliv Frame
            if (sender is Border border && border.BindingContext is RecipeWithCost selectedItem)
            {
                await Navigation.PushAsync(new RecipeDetailPage(selectedItem));
            }
        }

        private async void OnSettingsToolbarClicked(object sender, EventArgs e) //button settings
        {
            await Navigation.PushAsync(new SettingsPage());
        }

        // Tlačítko "Hledat" v patičce - otevře stránku pro hledání receptů na INTERNETU (SearchPage).
        // Nezaměňovat s lokálním vyhledávacím řádkem výše (OnLocalSearchButtonClicked), který hledá
        // jen mezi recepty už uloženými v appce.
        private async void OnSearchPageClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SearchPage());
        }

        private async void OnBookmarksClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new BookmarksPage());
        }

        // Vyhledávací řádek nahoře na stránce - hledá POUZE mezi lokálně uloženými recepty
        // (včetně receptů naimportovaných z internetu přes SearchPage), ne na internetu.
        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            await PerformSearchAsync();
        }

        private async void OnFilterByPreferencesChanged(object sender, CheckedChangedEventArgs e)
        {
            await PerformSearchAsync();
        }

        private async void OnLocalSearchButtonClicked(object sender, EventArgs e)
        {
            await PerformSearchAsync();
        }

        private async Task PerformSearchAsync()
        {
            string searchText = SearchEntry.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                await LoadRecipesDataAsync(); // prázdné pole -> zpět na běžný seznam podle rozpočtu
                return;
            }

            var results = await _budgetService.SearchRecipesAsync(searchText, FilterByPreferencesCheckBox.IsChecked);
            RecipesCollectionView.ItemsSource = results;
        }

        private async void OnResetDatabaseClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlertAsync(Tr("Reset databáze"), Tr("Tohle smaže VŠECHNY recepty a záložky a nahradí je testovacími daty. Pokračovat?"), Tr("Ano"), Tr("Zrušit"));
            if (!confirm) return;

            await _budgetService.ResetDatabaseAsync();
            await LoadRecipesDataAsync();
            await DisplayAlertAsync(Tr("Hotovo"), Tr("Databáze byla resetována na testovací data."), "OK");
        }

        private async void OnRepairStepsClicked(object sender, EventArgs e)
        {
            int fixedCount = await _budgetService.RepairAllRecipeStepsAsync();
            await DisplayAlertAsync(Tr("Hotovo"), string.Format(Tr("Opraveno receptů: {0}"), fixedCount), "OK");
        }

        private async Task HandlePendingImportAsync(string guid) //link-sharing
        {
            var recipe = await RecipeLinkShareService.ImportFromLinkAsync(guid);
            if (recipe != null)
            {
                int newId = await _budgetService.ImportSharedRecipeAsync(recipe);
                await _budgetService.AddRecipeToCategoryAsync(newId, "Vytvořené recepty");
                await DisplayAlertAsync(Tr("Recept naimportován"), string.Format(Tr("\"{0}\" byl přidán do tvých receptů."), recipe.Name), "OK");
                await LoadRecipesDataAsync();
            }
            else
            {
                await DisplayAlertAsync(Tr("Odkaz neplatný"), Tr("Tento odkaz na recept už není platný (buď vypršel, nebo už byl použit)."), "OK");
            }
        }
    }
}