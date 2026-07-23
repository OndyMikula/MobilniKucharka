#nullable disable
using MobilniKucharka.Classes.Recipe;
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

        // Zavolá se pokaždé, když se stránka zobrazí na displeji
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadRecipesDataAsync();
            UpdateSummaryUI();
            ResetDatabaseButton.IsVisible = Preferences.Default.Get("IsDeveloperMode", false);
            _ = CheckForUpdatesAsync();

            // Levný "nudge" pro překreslení - žádné čekání, žádný přepočet layoutu.
            Opacity = 0.999;
            Opacity = 1;
        }

        private async Task CheckForUpdatesAsync()
        {
            var updateService = new UpdateCheckService();
            var info = await updateService.CheckForUpdateAsync();

            if (info != null && info.IsUpdateAvailable)
            {
                bool download = await DisplayAlert(
                    "Nová verze je k dispozici",
                    $"Je dostupná nová verze aplikace ({info.LatestVersion}). Chceš ji nainstalovat? Všechny recepty, záložky a nastavení zůstanou zachovány.",
                    "Instalovat",
                    "Pokračovat bez instalace");

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
                await DisplayAlert("Chyba", "Nepodařilo se najít žádný recept. Zkontroluj internetové připojení.", "OK");
                return;
            }

            var savedRecipe = await _budgetService.SaveExternalRecipeAsync(found);

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
            int people = Preferences.Default.Get("PeopleCount", 2);
            double budget = Preferences.Default.Get("WeeklyBudget", 2000.0);

            SettingsSummaryLabel.Text = $"{people} osoby | Rozpočet: {budget} Kč/týden";
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

        private async void OnBookmarksClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new BookmarksPage());
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            await PerformSearchAsync();
        }

        private async void OnFilterByPreferencesChanged(object sender, CheckedChangedEventArgs e)
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

        private async void OnSearchButtonClicked(object sender, EventArgs e)
        {
            await PerformSearchAsync();
        }

        private async void OnResetDatabaseClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Reset databáze", "Tohle smaže VŠECHNY recepty a záložky a nahradí je testovacími daty. Pokračovat?", "Ano", "Zrušit");
            if (!confirm) return;

            await _budgetService.ResetDatabaseAsync();
            await LoadRecipesDataAsync();
            await DisplayAlert("Hotovo", "Databáze byla resetována na testovací data.", "OK");
        }
    }
}
