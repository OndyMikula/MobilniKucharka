using MobilniKucharka.Classes.Navigation;
using MobilniKucharka.Services;

namespace MobilniKucharka.Classes.Recipe
{
    public partial class SearchPage : ContentPage
    {
        private readonly RecipeSearchService _searchService;
        private CancellationTokenSource? _searchCts;
        private bool _isManualCancel;

        private static string Tr(string csText) => MobilniKucharka.Translation.UiTranslator.Tr(csText);

        public SearchPage()
        {
            InitializeComponent();
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "kucharka.db3");
            _searchService = new RecipeSearchService(dbPath);
            BottomNav.SetActiveTab(AppTab.Search);
        }

        private async void OnSearchButtonClicked(object sender, EventArgs e)
        {
            string query = SearchEntry.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query)) return;

            _isManualCancel = false;
            _searchCts = new CancellationTokenSource();
            _searchCts.CancelAfter(TimeSpan.FromSeconds(30));

            // Nové hledání vždy přepíše/smaže předchozí výsledky - a taky recepty, které si předchozí
            // hledání dočasně uložilo do DB přes "Detail" (a uživatel je nenaimportoval). Recepty
            // naimportované tlačítkem "Importovat" tímhle nejsou dotčené (viz MarkRecipeSearchTempAsync
            // a BudgetPlannerService.DeleteSearchTempRecipesAsync).
            await App.Database.DeleteSearchTempRecipesAsync();

            ResultsCollectionView.ItemsSource = null;
            SearchBarGrid.IsEnabled = false;
            LoadingOverlay.IsVisible = true;

            try
            {
                var results = await _searchService.SearchAsync(query, FilterByPreferencesCheckBox.IsChecked, _searchCts.Token);

                if (results.Count == 0)
                {
                    await DisplayAlertAsync(Tr("Nic se nenašlo"), Tr("Nepodařilo se najít žádný recept odpovídající zadání."), "OK");
                }
                else
                {
                    ResultsCollectionView.ItemsSource = results;
                }
            }
            catch (OperationCanceledException)
            {
                if (!_isManualCancel)
                {
                    await DisplayAlertAsync(Tr("Nic se nenašlo"), Tr("Vyhledávání trvalo příliš dlouho. Zkontroluj internetové připojení a zkus to znovu."), "OK");
                }
                // manuální zrušení tlačítkem - žádná hláška, uživatel to udělal záměrně
            }
            finally
            {
                LoadingOverlay.IsVisible = false;
                SearchBarGrid.IsEnabled = true;
                _searchCts?.Dispose();
                _searchCts = null;
            }
        }

        private void OnCancelSearchClicked(object sender, EventArgs e)
        {
            _isManualCancel = true;
            _searchCts?.Cancel();
        }

        // Patička (BottomNavBar) požádala o přepnutí na jinou hlavní záložku.
        private async void OnBottomNavTabRequested(object sender, AppTab tab)
        {
            await AppTabNavigation.GoToTabAsync(Navigation, tab);
        }

        private async void OnResultDetailClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn || btn.CommandParameter is not ExternalRecipeSearchResult result) return;
            await OpenRecipeAsync(result, isImport: false);
        }

        private async void OnResultImportClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn || btn.CommandParameter is not ExternalRecipeSearchResult result) return;
            await OpenRecipeAsync(result, isImport: true);
        }

        private async Task OpenRecipeAsync(ExternalRecipeSearchResult result, bool isImport)
        {
            try
            {
                var savedRecipe = await ResolveAndSaveRecipeAsync(result);
                if (savedRecipe == null)
                {
                    await DisplayAlertAsync(Tr("Chyba"), Tr("Recept se nepodařilo načíst. Zkontroluj internetové připojení."), "OK");
                    return;
                }

                // Recept zobrazený jen přes Detail zůstává dočasný (smaže se při dalším hledání) -
                // naimportovaný recept se označí jako trvalý a příštímu úklidu unikne. Nastavujeme
                // i lokální kopii (savedRecipe.IsSearchTemp), ne jen řádek v DB.
                await App.Database.MarkRecipeSearchTempAsync(savedRecipe.Id, isTemp: !isImport);
                savedRecipe.IsSearchTemp = !isImport;

                if (savedRecipe.ServingSize <= 0)
                {
                    string promptResult = await DisplayPromptAsync(
                        Tr("Pro kolik lidí by měl recept být?"),
                        Tr("Zadej, pro kolik lidí chceš recept uvařit."),
                        "OK", initialValue: string.Empty, keyboard: Keyboard.Numeric);

                    if (int.TryParse(promptResult, out var parsed) && parsed > 0)
                    {
                        await App.Database.UpdateRecipeServingSizeAsync(savedRecipe.Id, parsed);
                        savedRecipe.ServingSize = parsed;
                    }
                }

                if (isImport)
                {
                    await App.Database.AddRecipeToCategoryAsync(savedRecipe.Id, "Vytvořené recepty");
                }

                var recipeWithCost = new RecipeWithCost
                {
                    Recipe = savedRecipe,
                    CalculatedCost = 0, // externí zdroje neposkytují ceny surovin
                    IsWithinBudget = true
                };

                await Navigation.PushAsync(new RecipeDetailPage(recipeWithCost));
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(Tr("Chyba"), $"{Tr("Recept se nepodařilo uložit.")}\n{Tr("Detail")}: {ex.Message}", "OK");
            }
        }

        private async Task<Recipe?> ResolveAndSaveRecipeAsync(ExternalRecipeSearchResult result)
        {
            if (result.Source == ExternalRecipeSource.MealDb && result.MealDbData != null)
            {
                var completed = await _searchService.CompleteMealDbResultAsync(result);
                if (completed == null) return null;
                return await App.Database.SaveExternalRecipeAsync(completed);
            }

            if (result.Source == ExternalRecipeSource.Spoonacular && int.TryParse(result.ExternalId, out int spoonacularId))
            {
                return await _searchService.GetSpoonacularRecipeAsync(spoonacularId);
            }

            return null;
        }
    }
}