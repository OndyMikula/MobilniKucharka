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

    // Lehký záznam pro zobrazení v seznamu výsledků hledání (SearchPage). Name je vždy kanonický
    // anglický název ze zdrojového API (potřebný pro Name_EN při uložení) - NameCs, pokud existuje,
    // je jen pro zobrazení a pro znovupoužití při uložení receptu (viz DisplayName a
    // BudgetPlannerService.SaveExternalRecipeAsync/SpoonacularService.GetRecipeWithCacheAsync).
    public class ExternalRecipeSearchResult
    {
        public ExternalRecipeSource Source { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? NameCs { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public MealDbRecipe? MealDbData { get; set; }

        public string DisplayName => !string.IsNullOrWhiteSpace(NameCs) ? NameCs : Name;
    }

    // Sjednocuje hledání receptů na internetu napříč TheMealDB a Spoonacularem pro SearchPage.
    // Dotaz se před odesláním přeloží do angličtiny (obě API jsou anglická) - díky tomu funguje
    // hledání i pro česky napsané názvy receptů, ne jen anglické. Zobrazované názvy výsledků se
    // (v českém režimu appky) přeloží zpátky pro zobrazení - viz TranslateResultNamesForDisplayAsync.
    // Obě strany překladu (dotaz i výsledky) se cachují, ať appka za tutéž větu neplatí DeepL
    // kvótu opakovaně - viz BudgetPlannerService.GetSearchQueryTranslationAsync a
    // ExternalRecipeSearchResult.NameCs, který se propisuje přímo do uloženého receptu.
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

        public async Task<MealDbRecipe?> CompleteMealDbResultAsync(ExternalRecipeSearchResult result)
        {
            if (result.Source != ExternalRecipeSource.MealDb || result.MealDbData == null) return null;
            return await _mealDbService.CompleteRecipeWithNutritionAsync(result.MealDbData);
        }

        // translatedNameCs: propíše se přímo do Name_CS uloženého receptu, pokud appka během
        // hledání recept už přeložila pro zobrazení - viz ExternalRecipeSearchResult.NameCs.
        public async Task<Recipe?> GetSpoonacularRecipeAsync(int spoonacularId, string? translatedNameCs = null)
        {
            return await _spoonacularService.GetRecipeWithCacheAsync(spoonacularId, translatedNameCs);
        }

        // Přeloží dotaz do angličtiny, pokud appka běží v češtině - obě externí API rozumí prakticky
        // jen anglickým názvům. Nejdřív zkontroluje cache (stejný dotaz už dřív přeložený), teprve
        // pak zavolá DeepL - a výsledek si pro příště uloží. V anglickém režimu appky se žádný
        // překlad nevolá vůbec.
        private async Task<string> TranslateQueryToEnglishAsync(string query)
        {
            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            if (currentLang != "cs") return query;

            string? cached = await App.Database.GetSearchQueryTranslationAsync(query);
            if (!string.IsNullOrWhiteSpace(cached)) return cached;

            string? translated = await _translationService.TranslateAsync(query, targetAppLang: "en", sourceAppLang: "cs");
            if (string.IsNullOrWhiteSpace(translated)) return query;

            await App.Database.SaveSearchQueryTranslationAsync(query, translated);
            return translated;
        }

        // Přeloží zobrazované názvy nalezených receptů do aktuálního jazyka appky (v českém režimu
        // zpátky z angličtiny) - ukládá se do NameCs, NE do Name (Name zůstává kanonický anglický
        // název ze zdroje, potřebný pro Name_EN při uložení - viz ResolveAndSaveRecipeAsync).
        // Nejdřív zkusí jedno dávkové volání pro celý seznam najednou (šetří DeepL kvótu); pokud
        // tohle selže nebo vrátí neočekávaný počet položek, zkusí to znovu recept po receptu.
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
                        results[i].NameCs = translated[i];
                }
                return;
            }

            foreach (var result in results)
            {
                string? singleTranslated = await _translationService.TranslateAsync(result.Name, targetAppLang: "cs", sourceAppLang: "en");
                if (!string.IsNullOrWhiteSpace(singleTranslated))
                    result.NameCs = singleTranslated;
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
            string? diet = userDiets.Contains("Vegan") ? "vegan" : userDiets.Contains("Vegetarian") ? "vegetarian" : null;
            return await _spoonacularService.SearchRecipesAsync(query, diet, cancellationToken);
        }
    }
}