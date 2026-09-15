using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Translation;
using System.Diagnostics;

namespace MobilniKucharka.Services
{
    public partial class BudgetPlannerService
    {
        private bool _isTranslationCacheReady;

        private async Task EnsureTranslationCacheReadyAsync()
        {
            if (_isTranslationCacheReady) return;
            await _db.CreateTableAsync<RecipeTranslationCache>();
            await _db.CreateTableAsync<SearchQueryTranslationCache>();
            _isTranslationCacheReady = true;
        }

        public async Task<string?> GetTranslationCacheAsync(int recipeId, string fieldName, string languageCode)
        {
            await EnsureTranslationCacheReadyAsync();

            var entry = await _db.Table<RecipeTranslationCache>()
                .Where(c => c.RecipeId == recipeId && c.FieldName == fieldName && c.LanguageCode == languageCode)
                .FirstOrDefaultAsync();

            return entry?.TranslatedText;
        }

        public async Task SaveTranslationCacheAsync(int recipeId, string fieldName, string languageCode, string text)
        {
            await EnsureTranslationCacheReadyAsync();

            var existing = await _db.Table<RecipeTranslationCache>()
                .Where(c => c.RecipeId == recipeId && c.FieldName == fieldName && c.LanguageCode == languageCode)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.TranslatedText = text;
                await _db.UpdateAsync(existing);
            }
            else
            {
                await _db.InsertAsync(new RecipeTranslationCache
                {
                    RecipeId = recipeId,
                    FieldName = fieldName,
                    LanguageCode = languageCode,
                    TranslatedText = text
                });
            }
        }

        // Cache pro překlad vyhledávacích dotazů - viz SearchQueryTranslationCache.cs. Nezávislé
        // na konkrétním receptu, takže žije mimo RecipeId-scoped metody výše.
        public async Task<string?> GetSearchQueryTranslationAsync(string originalText)
        {
            await EnsureTranslationCacheReadyAsync();

            var entry = await _db.Table<SearchQueryTranslationCache>()
                .Where(c => c.OriginalText == originalText)
                .FirstOrDefaultAsync();

            return entry?.TranslatedText;
        }

        public async Task SaveSearchQueryTranslationAsync(string originalText, string translatedText)
        {
            await EnsureTranslationCacheReadyAsync();

            var existing = await _db.Table<SearchQueryTranslationCache>()
                .Where(c => c.OriginalText == originalText)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.TranslatedText = translatedText;
                await _db.UpdateAsync(existing);
            }
            else
            {
                await _db.InsertAsync(new SearchQueryTranslationCache
                {
                    OriginalText = originalText,
                    TranslatedText = translatedText
                });
            }
        }

        // Přeloží recept z jednoho jazyka do druhého a rovnou uloží.
        // skipName: true když Name_CS/EN už je vyplněné z jiného zdroje (viz SaveExternalRecipeAsync/
        // GetRecipeWithCacheAsync, kam se propisuje překlad hotový už během hledání v SearchPage) -
        // v tom případě se do dávkového DeepL volání jméno vůbec nezahrne a přeloží se jen kroky,
        // ať se tatáž věta nepřekládá (a neplatí) podruhé.
        public async Task<bool> TranslateAndSaveRecipeAsync(int recipeId, string fromLang, string toLang, bool skipName = false)
        {
            try
            {
                await EnsureInitializedAsync();

                var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
                if (recipe == null) return false;

                await SaveTranslationCacheAsync(recipeId, "DescriptionText", fromLang, recipe.DescriptionText);
                await SaveTranslationCacheAsync(recipeId, "IngredientsRaw", fromLang, recipe.IngredientsRaw);

                bool namesStepsOk = await TranslationService.TranslateRecipeNameAndStepsAsync(recipe, fromLang, toLang, skipName);
                if (!namesStepsOk) return false;

                recipe.DescriptionText = await GetOrTranslateFieldAsync(recipeId, "DescriptionText", recipe.DescriptionText, fromLang, toLang);
                recipe.IngredientsRaw = await GetOrTranslateFieldAsync(recipeId, "IngredientsRaw", recipe.IngredientsRaw, fromLang, toLang);
                recipe.ContentLanguage = toLang;

                await _db.UpdateAsync(recipe);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při překladu receptu: {ex.Message}");
                return false;
            }
        }

        private async Task<string> GetOrTranslateFieldAsync(int recipeId, string fieldName, string sourceText, string fromLang, string toLang)
        {
            string? cached = await GetTranslationCacheAsync(recipeId, fieldName, toLang);
            if (cached != null) return cached;

            if (string.IsNullOrWhiteSpace(sourceText)) return sourceText;

            string? translated = await TranslationService.TranslateAsync(sourceText, toLang, fromLang);
            if (string.IsNullOrWhiteSpace(translated)) return sourceText;

            await SaveTranslationCacheAsync(recipeId, fieldName, toLang, translated);
            return translated;
        }

        // Zajistí, že recept má vyplněný text v aktuálně nastaveném jazyce aplikace.
        // Pokud chybí (např. recept naimportovaný jen v angličtině a appka běží v češtině),
        // automaticky ho přeloží a uloží - bez nutnosti ručně mačkat "Přeložit".
        // Díky cache (viz TranslateAndSaveRecipeAsync) se tohle pro daný recept stane jen jednou navždy.

        public async Task<Recipe?> EnsureRecipeLanguageAsync(int recipeId) =>
    await EnsureRecipeLanguageAsync(recipeId, null);

        public async Task<Recipe?> EnsureRecipeLanguageAsync(int recipeId, Recipe? preloaded)
        {
            await EnsureInitializedAsync();

            var recipe = preloaded ?? await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe == null) return null;

            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            string otherLang = currentLang == "cs" ? "en" : "cs";

            string currentName = currentLang == "cs" ? recipe.Name_CS : recipe.Name_EN;
            string otherName = otherLang == "cs" ? recipe.Name_CS : recipe.Name_EN;
            var currentSteps = currentLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;
            var otherSteps = otherLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;

            bool nameOk = !string.IsNullOrWhiteSpace(currentName);
            bool stepsOk = otherSteps.Count == 0 || currentSteps.Count > 0;
            bool contentOk = recipe.ContentLanguage == currentLang;
            if (nameOk && stepsOk && contentOk)
                return recipe;

            if (string.IsNullOrWhiteSpace(otherName))
                return recipe;

            bool success = await TranslateAndSaveRecipeAsync(recipeId, fromLang: otherLang, toLang: currentLang, skipName: nameOk);
            if (!success) return recipe;

            return await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
        }

        // Ruční vynucení překladu (Možnosti receptu > Přeložit recept znovu) - pro recepty, kde
        // IngredientsRaw/DescriptionText obsahují smíchaný jazyk (typicky vlastní/sdílený recept),
        // což automatická kontrola výše neumí spolehlivě odhalit.
        public async Task<Recipe?> ForceRetranslateRecipeAsync(int recipeId)
        {
            await EnsureInitializedAsync();

            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe == null) return null;

            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            string otherLang = currentLang == "cs" ? "en" : "cs";
            string otherName = otherLang == "cs" ? recipe.Name_CS : recipe.Name_EN;

            if (string.IsNullOrWhiteSpace(otherName)) return recipe;

            await TranslateAndSaveRecipeAsync(recipeId, fromLang: otherLang, toLang: currentLang, skipName: false);
            return await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
        }
    }
}