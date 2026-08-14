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

        // Přeloží recept z jednoho jazyka do druhého a rovnou uloží.
        // Name_CS/EN a Steps_CS/EN se přeloží vždy přes DeepL (mají vlastní sloupce, žádná cache netřeba).
        // DescriptionText/IngredientsRaw (jednojazyčná pole) nejdřív zkontrolují cache - pokud tam cílový
        // jazyk už je z předchozího překladu, DeepL se pro ně vůbec nevolá.
        public async Task<bool> TranslateAndSaveRecipeAsync(int recipeId, string fromLang, string toLang)
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

                bool namesStepsOk = await translationService.TranslateRecipeNameAndStepsAsync(recipe, fromLang, toLang);
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
            bool hasCurrentLang = currentLang == "cs"
                ? !string.IsNullOrWhiteSpace(recipe.Name_CS)
                : !string.IsNullOrWhiteSpace(recipe.Name_EN);

            if (hasCurrentLang) return recipe; // není co dělat

            string otherLang = currentLang == "cs" ? "en" : "cs";
            bool hasOtherLang = otherLang == "cs"
                ? !string.IsNullOrWhiteSpace(recipe.Name_CS)
                : !string.IsNullOrWhiteSpace(recipe.Name_EN);

            if (!hasOtherLang) return recipe; // recept nemá vyplněný ani jeden jazyk, není z čeho překládat

            bool success = await TranslateAndSaveRecipeAsync(recipeId, fromLang: otherLang, toLang: currentLang);
            if (!success) return recipe; // překlad selhal (offline / limit) - vrátíme aspoň to, co je

            return await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
        }
    }
}