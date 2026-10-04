using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Services.Cache;
using MobilniKucharka.Services.Recipes;
using MobilniKucharka.Services.Utilities;
using MobilniKucharka.Translation;
using MobilniKucharka.Translation.Content;
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
        private static async Task<bool> EnsureNameAndStepsFilledAsync(Recipe recipe, string currentLang, string otherLang)
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
            List<string>? translatedSteps = stepsMissing ? [.. translated.Skip(idx)] : null;

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

        // Suroviny a popis se teď kontrolují a překládají NEZÁVISLE - recept může mít suroviny
        // anglicky a popis česky zároveň, žádný z nich se nesmí nechat "svézt" na jazyku toho druhého.
        private async Task<Recipe> BuildDisplayRecipeAsync(Recipe recipe, string currentLang)
        {
            string? descText = null;
            string? ingrText = null;

            if (!string.IsNullOrWhiteSpace(recipe.DescriptionLanguage) && recipe.DescriptionLanguage != currentLang)
            {
                string? cachedDesc = await GetTranslationCacheAsync(recipe.Id, "DescriptionText", currentLang);
                if (cachedDesc != null && IsEchoOfSource(cachedDesc, recipe.DescriptionText)) cachedDesc = null;
                descText = cachedDesc ?? await TranslateFieldForDisplayAsync(recipe.Id, "DescriptionText", recipe.DescriptionText, recipe.DescriptionLanguage, currentLang);
            }

            if (!string.IsNullOrWhiteSpace(recipe.ContentLanguage) && recipe.ContentLanguage != currentLang)
            {
                string? cachedIngr = await GetTranslationCacheAsync(recipe.Id, "IngredientsRaw", currentLang);
                if (cachedIngr != null && IsEchoOfSource(cachedIngr, recipe.IngredientsRaw)) cachedIngr = null;
                ingrText = cachedIngr ?? await TranslateFieldForDisplayAsync(recipe.Id, "IngredientsRaw", recipe.IngredientsRaw, recipe.ContentLanguage, currentLang);
            }

            if (descText == null && ingrText == null) return recipe;

            var display = recipe.ShallowClone();
            if (descText != null) display.DescriptionText = descText;
            if (ingrText != null) display.IngredientsRaw = ingrText;
            return display;
        }

        private static bool IsEchoOfSource(string cachedText, string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText)) return false;
            return TextNormalizationHelper.NormalizeForComparison(cachedText) == TextNormalizationHelper.NormalizeForComparison(sourceText);
        }

        // Vrací počet receptů, kterým se nastavil ContentLanguage - importované (jistota "en") a
        // vlastní recepty bez ContentLanguage (odhad podle diakritiky v IngredientsRaw/
        // DescriptionText, jediný dostupný signál u starých receptů z doby před tímhle polem).
        // Jednorázové navždy - po prvním běhu už žádný recept nezůstane bez ContentLanguage.
        // Verze v4 - přidává DescriptionLanguage vedle ContentLanguage, takže se spustí ještě
        // jednou i u receptů, které už v3 proběhly.
        public async Task<int> EnsureContentLanguageMigrationAsync()
        {
            await EnsureInitializedAsync();

            const string prefKey = "ContentLanguageMigrationDone_v4";
            if (Preferences.Default.Get(prefKey, false)) return 0;

            int fixedCount = 0;

            var imported = await _db.Table<Recipe>().Where(r => r.ExternalSourceId != "").ToListAsync();
            foreach (var recipe in imported)
            {
                bool changed = false;
                if (recipe.ContentLanguage != "en") { recipe.ContentLanguage = "en"; changed = true; }
                if (recipe.DescriptionLanguage != "en") { recipe.DescriptionLanguage = "en"; changed = true; }
                if (changed) { await _db.UpdateAsync(recipe); fixedCount++; }
            }

            var ownRecipes = await _db.Table<Recipe>().Where(r => r.ExternalSourceId == "").ToListAsync();
            foreach (var recipe in ownRecipes)
            {
                bool changed = false;

                if (string.IsNullOrWhiteSpace(recipe.ContentLanguage))
                {
                    recipe.ContentLanguage = ContainsCzechDiacritics(recipe.IngredientsRaw) ? "cs" : "en";
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(recipe.DescriptionLanguage))
                {
                    recipe.DescriptionLanguage = string.IsNullOrWhiteSpace(recipe.DescriptionText)
                        ? recipe.ContentLanguage
                        : (ContainsCzechDiacritics(recipe.DescriptionText) ? "cs" : "en");
                    changed = true;
                }

                if (changed) { await _db.UpdateAsync(recipe); fixedCount++; }
            }

            Preferences.Default.Set(prefKey, true);
            return fixedCount;
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

        // Přeloží suroviny a popis nezávisle, každé ze svého vlastního uloženého jazyka - žádné
        // hádání, žádná ptaní se uživatele. "Opravit jazyk receptu" (SetRecipeLanguagesAsync níž)
        // je jediné místo, kde se ty dva zdrojové jazyky ručně mění.
        public async Task<Recipe?> ForceRetranslateRecipeAsync(int recipeId)
        {
            await EnsureInitializedAsync();

            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe == null) return null;

            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            string otherLang = currentLang == "cs" ? "en" : "cs";

            string ingredientsSourceLang = string.IsNullOrWhiteSpace(recipe.ContentLanguage) ? otherLang : recipe.ContentLanguage;
            string descriptionSourceLang = string.IsNullOrWhiteSpace(recipe.DescriptionLanguage) ? otherLang : recipe.DescriptionLanguage;

            if (ingredientsSourceLang != currentLang)
            {
                string? translatedIngr = await TranslationService.TranslateAsync(recipe.IngredientsRaw, currentLang, ingredientsSourceLang);
                if (!string.IsNullOrWhiteSpace(translatedIngr))
                    await SaveTranslationCacheAsync(recipeId, "IngredientsRaw", currentLang, translatedIngr);
            }

            if (descriptionSourceLang != currentLang)
            {
                string? translatedDesc = await TranslationService.TranslateAsync(recipe.DescriptionText, currentLang, descriptionSourceLang);
                if (!string.IsNullOrWhiteSpace(translatedDesc))
                    await SaveTranslationCacheAsync(recipeId, "DescriptionText", currentLang, translatedDesc);
            }

            string nameSourceLang = otherLang;
            string sourceName = nameSourceLang == "cs" ? recipe.Name_CS : recipe.Name_EN;
            var sourceSteps = nameSourceLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;

            if (!string.IsNullOrWhiteSpace(sourceName))
            {
                var batch = new List<string> { sourceName };
                batch.AddRange(sourceSteps);

                var translated = await TranslationService.TranslateBatchAsync(batch, currentLang, nameSourceLang);
                if (translated != null && translated.Count == batch.Count)
                {
                    if (currentLang == "cs")
                    {
                        recipe.Name_CS = translated[0];
                        recipe.Steps_CS = [.. translated.Skip(1)];
                    }
                    else
                    {
                        recipe.Name_EN = translated[0];
                        recipe.Steps_EN = [.. translated.Skip(1)];
                    }
                    await _db.UpdateAsync(recipe);
                }
            }

            RecipeListCache.Invalidate();
            return await BuildDisplayRecipeAsync(recipe, currentLang);
        }

        // Ruční oprava obou jazykových značek nezávisle. Zahodí i příslušnou cache překladu -
        // ta mohla vzniknout z chybného předpokladu a nesmí přežít opravu.
        public async Task<Recipe?> SetRecipeLanguagesAsync(int recipeId, string ingredientsLanguage, string descriptionLanguage)
        {
            await EnsureInitializedAsync();

            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe == null) return null;

            await EnsureTranslationCacheReadyAsync();

            recipe.ContentLanguage = ingredientsLanguage;
            var ingrCache = await _db.Table<RecipeTranslationCache>().Where(c => c.RecipeId == recipeId && c.FieldName == "IngredientsRaw").ToListAsync();
            foreach (var entry in ingrCache) await _db.DeleteAsync(entry);

            recipe.DescriptionLanguage = descriptionLanguage;
            var descCache = await _db.Table<RecipeTranslationCache>().Where(c => c.RecipeId == recipeId && c.FieldName == "DescriptionText").ToListAsync();
            foreach (var entry in descCache) await _db.DeleteAsync(entry);

            await _db.UpdateAsync(recipe);
            RecipeListCache.Invalidate();
            return recipe;
        }

        public async Task<int> EnsureRequiredEquipmentMigrationAsync()
        {
            await EnsureInitializedAsync();

            const string prefKey = "RequiredEquipmentMigrationDone_v1";
            if (Preferences.Default.Get(prefKey, false)) return 0;

            var recipes = await _db.Table<Recipe>().ToListAsync();
            int fixedCount = 0;

            foreach (var recipe in recipes)
            {
                if (recipe.RequiredEquipmentJson != "[]") continue;

                string combined = string.Join(" ", recipe.Steps_CS.Concat(recipe.Steps_EN));
                var inferred = RequiredEquipmentAnalysisService.InferRequiredEquipment(combined);
                if (inferred.Count == 0) continue;

                recipe.RequiredEquipment = inferred;
                await _db.UpdateAsync(recipe);
                fixedCount++;
            }

            if (fixedCount > 0) RecipeListCache.Invalidate();
            Preferences.Default.Set(prefKey, true);
            return fixedCount;
        }

        // Ruční oprava, když appka špatně uhodla ContentLanguage (typicky: recept napsaný v jiném
        // jazyce, než v jakém byla zrovna UI appky při ukládání). Zahodí i starou cache překladu -
        // ta mohla vzniknout z chybného předpokladu a nesmí přežít opravu.
        public async Task<Recipe?> SetRecipeContentLanguageAsync(int recipeId, string newContentLanguage)
        {
            await EnsureInitializedAsync();

            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe == null) return null;

            recipe.ContentLanguage = newContentLanguage;
            await _db.UpdateAsync(recipe);

            await EnsureTranslationCacheReadyAsync();
            var cacheEntries = await _db.Table<RecipeTranslationCache>().Where(c => c.RecipeId == recipeId).ToListAsync();
            foreach (var entry in cacheEntries)
                await _db.DeleteAsync(entry);

            return recipe;
        }
    }
}