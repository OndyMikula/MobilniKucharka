using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Services.Api;
using MobilniKucharka.Translation;

namespace MobilniKucharka.Services
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
    // hledání i pro česky napsané názvy receptů, ne jen anglické. Zobrazované názvy výsledků se
    // (v českém režimu appky) přeloží zpátky pro zobrazení v SearchPage - viz
    // TranslateResultNamesForDisplayAsync.
    public class RecipeSearchService(string dbPath)
    {
        private readonly TheMealDbService _mealDbService = new();
        private readonly SpoonacularService _spoonacularService = new(dbPath);
        private readonly TranslationService _translationService = new();

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

            await TranslateResultNamesForDisplayAsync(combined);

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

        // Přeloží zobrazované názvy nalezených receptů do aktuálního jazyka appky (v českém režimu
        // zpátky z angličtiny) - jen pro zobrazení v SearchPage, ne trvale (výsledky hledání ještě
        // nejsou v DB). Skutečný Name_CS/EN se natrvalo doplní až při uložení (Detail/Import), viz
        // BudgetPlannerService.EnsureRecipeLanguageAsync. Nejdřív zkusí jedno dávkové volání pro
        // celý seznam najednou (šetří DeepL kvótu); pokud tohle selže nebo vrátí neočekávaný počet
        // položek (výpadek/timeout uprostřed požadavku), zkusí to znovu recept po receptu, ať aspoň
        // část seznamu vyjde přeložená místo toho, aby kvůli jedné chybě zůstal celý seznam anglicky.
        private async Task TranslateResultNamesForDisplayAsync(List<ExternalRecipeSearchResult> results)
        {
            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            if (currentLang != "cs" || results.Count == 0) return; // zdroje jsou anglicky - v EN režimu není co překládat

            var names = results.Select(r => r.Name).ToList();
            var translated = await _translationService.TranslateBatchAsync(names, targetAppLang: "cs", sourceAppLang: "en");

            if (translated != null && translated.Count == results.Count)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(translated[i]))
                        results[i].Name = translated[i];
                }
                return;
            }

            foreach (var result in results)
            {
                string? singleTranslated = await _translationService.TranslateAsync(result.Name, targetAppLang: "cs", sourceAppLang: "en");
                if (!string.IsNullOrWhiteSpace(singleTranslated))
                    result.Name = singleTranslated;
            }
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