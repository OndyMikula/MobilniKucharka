using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Translation;

namespace MobilniKucharka.Services.Api
{
    public enum ExternalRecipeSource
    {
        MealDb,
        Spoonacular
    }

    // Lehký záznam pro zobrazení v seznamu výsledků hledání (SearchPage). U MealDB obsahuje rovnou
    // kompletní data (search.php je vrací najednou, viz TheMealDbService.SearchByNameAsync) - u
    // Spoonacularu se plné detaily dotahují až po výběru (viz SpoonacularService.GetRecipeWithCacheAsync).
    public class ExternalRecipeSearchResult
    {
        public ExternalRecipeSource Source { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public MealDbRecipe? MealDbData { get; set; }
    }

    // Sjednocuje hledání receptů na internetu napříč TheMealDB a Spoonacularem pro SearchPage.
    // Dotaz se před odesláním přeloží do angličtiny (obě API jsou anglická) - díky tomu funguje
    // hledání i pro česky napsané názvy receptů, ne jen anglické.
    public class RecipeSearchService
    {
        private readonly TheMealDbService _mealDbService = new();
        private readonly SpoonacularService _spoonacularService;
        private readonly TranslationService _translationService = new();

        public RecipeSearchService(string dbPath)
        {
            _spoonacularService = new SpoonacularService(dbPath);
        }

        public async Task<List<ExternalRecipeSearchResult>> SearchAsync(string rawQuery, bool applyDietFilter, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rawQuery)) return [];

            string englishQuery = await TranslateQueryToEnglishAsync(rawQuery.Trim());
            cancellationToken.ThrowIfCancellationRequested();

            List<string> userDiets = applyDietFilter ? ParseUserDiets() : [];

            var mealDbTask = SearchMealDbAsync(englishQuery, userDiets, cancellationToken);
            var spoonacularTask = SearchSpoonacularAsync(englishQuery, userDiets, cancellationToken);

            await Task.WhenAll(mealDbTask, spoonacularTask);

            var combined = new List<ExternalRecipeSearchResult>();
            combined.AddRange(mealDbTask.Result);
            combined.AddRange(spoonacularTask.Result);
            return combined;
        }

        // Dopočítá nutrici pro konkrétní MealDB výsledek vybraný uživatelem ze seznamu - viz
        // TheMealDbService.CompleteRecipeWithNutritionAsync (neřešíme to pro celý seznam, jen pro vybraný recept).
        public async Task<MealDbRecipe?> CompleteMealDbResultAsync(ExternalRecipeSearchResult result)
        {
            if (result.Source != ExternalRecipeSource.MealDb || result.MealDbData == null) return null;
            return await _mealDbService.CompleteRecipeWithNutritionAsync(result.MealDbData);
        }

        // Dotáhne (a rovnou uloží do lokální DB, viz SpoonacularService) plné detaily pro Spoonacular výsledek.
        public async Task<Recipe?> GetSpoonacularRecipeAsync(int spoonacularId)
        {
            return await _spoonacularService.GetRecipeWithCacheAsync(spoonacularId);
        }

        // Přeloží dotaz do angličtiny, pokud appka běží v češtině - obě externí API rozumí prakticky
        // jen anglickým názvům. V anglickém režimu appky se žádný překlad nevolá (šetříme DeepL kvótu).
        private async Task<string> TranslateQueryToEnglishAsync(string query)
        {
            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            if (currentLang != "cs") return query;

            string? translated = await _translationService.TranslateAsync(query, targetAppLang: "en", sourceAppLang: "cs");
            return string.IsNullOrWhiteSpace(translated) ? query : translated;
        }

        private static List<string> ParseUserDiets()
        {
            string raw = Preferences.Default.Get("UserDiets", "");
            return string.IsNullOrWhiteSpace(raw)
                ? []
                : [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }

        private async Task<List<ExternalRecipeSearchResult>> SearchMealDbAsync(string query, List<string> userDiets, CancellationToken cancellationToken)
        {
            var meals = await _mealDbService.SearchByNameAsync(query, cancellationToken);

            if (userDiets.Count > 0)
                meals = [.. meals.Where(m => TheMealDbService.GuessDietFlagsFromCategory(m.Category).Any(userDiets.Contains))];

            return [.. meals.Select(m => new ExternalRecipeSearchResult
            {
                Source = ExternalRecipeSource.MealDb,
                ExternalId = m.ExternalId,
                Name = m.Name,
                ImageUrl = m.ImageUrl,
                MealDbData = m
            })];
        }

        private async Task<List<ExternalRecipeSearchResult>> SearchSpoonacularAsync(string query, List<string> userDiets, CancellationToken cancellationToken)
        {
            // Spoonacular bere jen jeden "diet" parametr - při víc zaškrtnutých preferencích
            // vezmeme tu přísnější (vegan recepty jsou i vegetariánské, takže vegan filtr nikoho neochudí).
            string? diet = userDiets.Contains("Vegan") ? "vegan" : userDiets.Contains("Vegetarian") ? "vegetarian" : null;
            return await _spoonacularService.SearchRecipesAsync(query, diet, cancellationToken);
        }
    }
}