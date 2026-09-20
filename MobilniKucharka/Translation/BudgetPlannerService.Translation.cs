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

        // Doplní Name_CS/EN a Steps_CS/EN pro chybějící stranu - NIKDY nepřepisuje stranu, která už
        // obsahuje text (ať už od uživatele, nebo z dřívějšího překladu). Vrací true, pokud se něco
        // změnilo (a je tedy potřeba _db.UpdateAsync). Tohle je jediné místo, kde se tahle dvě pole
        // pro AUTOMATICKOU kontrolu (EnsureRecipeLanguageAsync) vůbec mění.
        private async Task<bool> EnsureNameAndStepsFilledAsync(Recipe recipe, string currentLang, string otherLang)
        {
            string currentName = currentLang == "cs" ? recipe.Name_CS : recipe.Name_EN;
            string otherName = otherLang == "cs" ? recipe.Name_CS : recipe.Name_EN;
            var currentSteps = currentLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;
            var otherSteps = otherLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;

            bool nameMissing = string.IsNullOrWhiteSpace(currentName);
            bool stepsMissing = otherSteps.Count > 0 && currentSteps.Count == 0;

            if (!nameMissing && !stepsMissing) return false;
            if (string.IsNullOrWhiteSpace(otherName)) return false;

            var batch = new List<string>();
            if (nameMissing) batch.Add(otherName);
            if (stepsMissing) batch.AddRange(otherSteps);
            if (batch.Count == 0) return false;

            var translated = await TranslationService.TranslateBatchAsync(batch, currentLang, otherLang);
            if (translated == null || translated.Count != batch.Count) return false;

            int idx = 0;
            string? translatedName = nameMissing ? translated[idx++] : null;
            List<string>? translatedSteps = stepsMissing ? translated.Skip(idx).ToList() : null;

            if (currentLang == "cs")
            {
                if (nameMissing) recipe.Name_CS = translatedName!;
                if (stepsMissing) recipe.Steps_CS = translatedSteps!;
            }
            else
            {
                if (nameMissing) recipe.Name_EN = translatedName!;
                if (stepsMissing) recipe.Steps_EN = translatedSteps!;
            }

            return true;
        }

        // Přeloží jedno pole pro ZOBRAZENÍ a uloží ho do cache - NIKDY nezapisuje zpět do recipe.*
        // sloupce. Selhání překladu vrátí zdrojový text (zobrazí se originál, ne prázdno).
        private async Task<string> TranslateFieldForDisplayAsync(int recipeId, string fieldName, string sourceText, string fromLang, string toLang)
        {
            if (string.IsNullOrWhiteSpace(sourceText)) return sourceText;

            string? translated = await TranslationService.TranslateAsync(sourceText, toLang, fromLang);
            if (string.IsNullOrWhiteSpace(translated))
            {
                Debug.WriteLine($"[Translate] Pole '{fieldName}' receptu {recipeId} se nepodařilo přeložit - zobrazuje se originál.");
                return sourceText;
            }

            await SaveTranslationCacheAsync(recipeId, fieldName, toLang, translated);
            return translated;
        }

        // Vrátí recept se správným jazykem pro ZOBRAZENÍ. Pokud recipe.ContentLanguage == currentLang
        // (nebo je prázdný - neznámý/starý recept), vrací recipe beze změny. Jinak vrátí MĚLKOU
        // KOPII s přeloženým IngredientsRaw/DescriptionText (z cache, nebo čerstvě přeloženým a
        // uloženým do cache) - originální řádek v DB se nikdy nezmění.
        private async Task<Recipe> BuildDisplayRecipeAsync(Recipe recipe, string currentLang)
        {
            if (string.IsNullOrWhiteSpace(recipe.ContentLanguage) || recipe.ContentLanguage == currentLang)
                return recipe;

            string? cachedDesc = await GetTranslationCacheAsync(recipe.Id, "DescriptionText", currentLang);
            string? cachedIngr = await GetTranslationCacheAsync(recipe.Id, "IngredientsRaw", currentLang);

            string descText = cachedDesc ?? await TranslateFieldForDisplayAsync(recipe.Id, "DescriptionText", recipe.DescriptionText, recipe.ContentLanguage, currentLang);
            string ingrText = cachedIngr ?? await TranslateFieldForDisplayAsync(recipe.Id, "IngredientsRaw", recipe.IngredientsRaw, recipe.ContentLanguage, currentLang);

            var display = recipe.ShallowClone();
            display.DescriptionText = descText;
            display.IngredientsRaw = ingrText;
            return display;
        }

        // Jednorázová migrace pro recepty založené před zavedením ContentLanguage - importované
        // recepty (MealDB/Spoonacular) jsou VŽDY anglicky u zdroje, takže se jim pole nastaví na
        // "en" napevno (ne prázdné - prázdné teď znamená "neznámé, nepřekládat", což by import
        // navždy zamrzlo v původním jazyce zobrazení).
        public async Task EnsureContentLanguageMigrationAsync()
        {
            const string prefKey = "ContentLanguageMigrationDone_v2";
            if (Preferences.Default.Get(prefKey, false)) return;

            var imported = await _db.Table<Recipe>().Where(r => r.ExternalSourceId != "").ToListAsync();

            foreach (var recipe in imported)
            {
                recipe.ContentLanguage = "en";
                await _db.UpdateAsync(recipe);
            }

            Preferences.Default.Set(prefKey, true);
        }

        // Automatická kontrola při každém zobrazení receptu. NIKDY nepřepisuje IngredientsRaw/
        // DescriptionText/ContentLanguage v DB - jen doplní chybějící Name/Steps stranu (persistuje
        // se, protože jde o legitimní bilingvní pár) a vrátí přeloženou DISPLAY kopii.
        public async Task<Recipe?> EnsureRecipeLanguageAsync(int recipeId) =>
            await EnsureRecipeLanguageAsync(recipeId, null);

        public async Task<Recipe?> EnsureRecipeLanguageAsync(int recipeId, Recipe? preloaded)
        {
            await EnsureInitializedAsync();

            var recipe = preloaded ?? await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe == null) return null;

            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            string otherLang = currentLang == "cs" ? "en" : "cs";

            bool changed = await EnsureNameAndStepsFilledAsync(recipe, currentLang, otherLang);
            if (changed)
                await _db.UpdateAsync(recipe);

            return await BuildDisplayRecipeAsync(recipe, currentLang);
        }

        // Ruční vynucení překladu (Možnosti receptu > Přeložit recept znovu) - přegeneruje cache
        // pro IngredientsRaw/DescriptionText a přepíše Name/Steps aktuálního jazyka (na explicitní
        // žádost uživatele, jinak se to nestane automaticky). Zdrojem je VŽDY recipe.ContentLanguage
        // (originál), nikdy dřívější překlad - takže opakované vynucení nikdy nedegraduje text.
        public async Task<Recipe?> ForceRetranslateRecipeAsync(int recipeId)
        {
            await EnsureInitializedAsync();

            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe == null) return null;

            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            string sourceLang = string.IsNullOrWhiteSpace(recipe.ContentLanguage) ? currentLang : recipe.ContentLanguage;

            if (sourceLang == currentLang)
                return recipe;

            string? translatedDesc = await TranslationService.TranslateAsync(recipe.DescriptionText, currentLang, sourceLang);
            if (!string.IsNullOrWhiteSpace(translatedDesc))
                await SaveTranslationCacheAsync(recipeId, "DescriptionText", currentLang, translatedDesc);

            string? translatedIngr = await TranslationService.TranslateAsync(recipe.IngredientsRaw, currentLang, sourceLang);
            if (!string.IsNullOrWhiteSpace(translatedIngr))
                await SaveTranslationCacheAsync(recipeId, "IngredientsRaw", currentLang, translatedIngr);

            string sourceName = sourceLang == "cs" ? recipe.Name_CS : recipe.Name_EN;
            var sourceSteps = sourceLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;

            if (!string.IsNullOrWhiteSpace(sourceName))
            {
                var batch = new List<string> { sourceName };
                batch.AddRange(sourceSteps);

                var translated = await TranslationService.TranslateBatchAsync(batch, currentLang, sourceLang);
                if (translated != null && translated.Count == batch.Count)
                {
                    if (currentLang == "cs")
                    {
                        recipe.Name_CS = translated[0];
                        recipe.Steps_CS = translated.Skip(1).ToList();
                    }
                    else
                    {
                        recipe.Name_EN = translated[0];
                        recipe.Steps_EN = translated.Skip(1).ToList();
                    }
                    await _db.UpdateAsync(recipe);
                }
            }

            return await BuildDisplayRecipeAsync(recipe, currentLang);
        }
    }
}