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

                // Uložíme aktuální (zdrojový) text do cache dřív, než ho případně přepíšeme -
                // DescriptionText/IngredientsRaw je na Recipe jen jedno pole, tohle je jediné místo,
                // kde obě jazykové verze přežijí.
                await SaveTranslationCacheAsync(recipeId, "DescriptionText", fromLang, recipe.DescriptionText);
                await SaveTranslationCacheAsync(recipeId, "IngredientsRaw", fromLang, recipe.IngredientsRaw);

                var translationService = new TranslationService();

                bool namesStepsOk = await translationService.TranslateRecipeNameAndStepsAsync(recipe, fromLang, toLang, skipName);
                if (!namesStepsOk) return false;

                recipe.DescriptionText = await GetOrTranslateFieldAsync(translationService, recipeId, "DescriptionText", recipe.DescriptionText, fromLang, toLang);
                recipe.IngredientsRaw = await GetOrTranslateFieldAsync(translationService, recipeId, "IngredientsRaw", recipe.IngredientsRaw, fromLang, toLang);

                await _db.UpdateAsync(recipe);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při překladu receptu: {ex.Message}");
                return false;
            }
        }

        // Vrátí přeložený text z cache, pokud tam pro daný jazyk už je; jinak zavolá DeepL
        // a výsledek do cache uloží pro příště (žádný recept se pak nepřekládá dvakrát).
        private async Task<string> GetOrTranslateFieldAsync(TranslationService translationService, int recipeId, string fieldName, string sourceText, string fromLang, string toLang)
        {
            string? cached = await GetTranslationCacheAsync(recipeId, fieldName, toLang);
            if (cached != null) return cached;

            if (string.IsNullOrWhiteSpace(sourceText)) return sourceText;

            string? translated = await translationService.TranslateAsync(sourceText, toLang, fromLang);
            if (string.IsNullOrWhiteSpace(translated)) return sourceText; // překlad selhal, necháme původní text

            await SaveTranslationCacheAsync(recipeId, fieldName, toLang, translated);
            return translated;
        }

        // Zajistí, že recept má vyplněný text v aktuálně nastaveném jazyce appky.
        // Pokud chybí (např. recept naimportovaný jen v angličtině a appka běží v češtině),
        // automaticky ho přeloží a uloží - bez nutnosti ručně mačkat "Přeložit".
        // Díky cache (viz TranslateAndSaveRecipeAsync) se tohle pro daný recept stane jen jednou navždy.
        public async Task<Recipe?> EnsureRecipeLanguageAsync(int recipeId)
        {
            await EnsureInitializedAsync();

            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe == null) return null;

            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            string otherLang = currentLang == "cs" ? "en" : "cs";

            string currentName = currentLang == "cs" ? recipe.Name_CS : recipe.Name_EN;
            string otherName = otherLang == "cs" ? recipe.Name_CS : recipe.Name_EN;
            var currentSteps = currentLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;
            var otherSteps = otherLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;

            bool nameOk = !string.IsNullOrWhiteSpace(currentName);

            // "Hotovo" znamená: jméno je vyplněné A (zdrojový jazyk nemá žádné kroky, NEBO cílový jazyk
            // kroky taky má). Dřív se kontrolovalo jen jméno, takže recept s vyplněným jménem ale prázdnými
            // kroky (např. z dřív přerušeného překladu) navždy vypadal jako hotový a nikdy se nedokončil.
            bool stepsOk = otherSteps.Count == 0 || currentSteps.Count > 0;
            if (nameOk && stepsOk)
                return recipe;

            if (string.IsNullOrWhiteSpace(otherName))
                return recipe; // není z čeho překládat

            // Jméno může už být hotové (recept uložený rovnou s Name_CS z RecipeSearchService, viz
            // TranslateResultNamesForDisplayAsync), zatímco kroky ještě ne - pak přeložíme jen kroky.
            bool success = await TranslateAndSaveRecipeAsync(recipeId, fromLang: otherLang, toLang: currentLang, skipName: nameOk);
            if (!success) return recipe;

            return await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
        }
    }
}