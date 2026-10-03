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

    public class RecipeSearchService(string dbPath)
    {
        private readonly SpoonacularService _spoonacularService = new(dbPath);

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
            return await TheMealDbService.CompleteRecipeWithNutritionAsync(result.MealDbData);
        }

        public async Task<Recipe?> GetSpoonacularRecipeAsync(int spoonacularId, string? translatedNameCs = null)
        {
            return await _spoonacularService.GetRecipeWithCacheAsync(spoonacularId, translatedNameCs);
        }

        private async Task<string> TranslateQueryToEnglishAsync(string query)
        {
            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            if (currentLang != "cs") return query;

            string? cached = await App.Database.GetSearchQueryTranslationAsync(query);
            if (!string.IsNullOrWhiteSpace(cached)) return cached;

            string? translated = await TranslationService.TranslateAsync(query, targetAppLang: "en", sourceAppLang: "cs");
            if (string.IsNullOrWhiteSpace(translated)) return query;

            await App.Database.SaveSearchQueryTranslationAsync(query, translated);
            return translated;
        }

        private async Task TranslateResultNamesForDisplayAsync(List<ExternalRecipeSearchResult> results)
        {
            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            if (currentLang != "cs" || results.Count == 0) return;

            var names = results.Select(r => r.Name).ToList();
            var translated = await TranslationService.TranslateBatchAsync(names, targetAppLang: "cs", sourceAppLang: "en");

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
                string? singleTranslated = await TranslationService.TranslateAsync(result.Name, targetAppLang: "cs", sourceAppLang: "en");
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

        private static async Task<List<ExternalRecipeSearchResult>> SearchMealDbAsync(string query, List<string> userDiets, CancellationToken cancellationToken)
        {
            var meals = await TheMealDbService.SearchByNameAsync(query, cancellationToken);

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
            return await SpoonacularService.SearchRecipesAsync(query, diet, cancellationToken);
        }
    }
}