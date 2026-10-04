using MobilniKucharka.Classes;
using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Services.Cache;
using MobilniKucharka.Services.Utilities;
using MobilniKucharka.Translation;
using MobilniKucharka.Translation.Content;

namespace MobilniKucharka.Services
{
    public partial class BudgetPlannerService
    {
        // Sloučí duplicitní/téměř duplicitní suroviny (jiná diakritika, velikost písmen, pomlčka
        // vs mezera, nebo stejná surovina zapsaná zvlášť česky a anglicky). Spouští se jednou manuálně
        // - co se nedá sloučit deterministicky nebo kvůli chybějící DeepL kvótě, zůstane
        // beze změny pro příští běh.
        // Na vlákně z thread poolu - nesmí blokovat UI vlákno Blazoru.
        public Task<int> MergeDuplicateProductsAsync() => Task.Run(MergeDuplicateProductsCoreAsync);

        private async Task<int> MergeDuplicateProductsCoreAsync()
        {
            await EnsureInitializedAsync();

            int merged = await ApplyKnownTranslationCorrectionsAsync();
            merged += await NormalizeProductCapitalizationAsync();
            merged += await MergeByNormalizedNameOverlapAsync();
            merged += await RunUntranslatedDuplicatesMigrationOnceAsync();

            _cachedProducts = null;
            _cachedAliases = null;
            if (merged > 0) RecipeListCache.Invalidate();
            return merged;
        }

        // DeepL-závislá fáze - na rozdíl od ostatních (zdarma, offline) spustit jen JEDNOU navždy,
        // ať se nespotřebovává sdílená celoživotní kvóta při každém spuštění appky. Nově vytvořené
        // duplicity už teď vznikat nemají (viz GetOrCreateLocalProductByNameAsync), takže jednou
        // stačí; co se nedopřeloží kvůli kvótě, lze opravit ručně v Nastavení > Suroviny (zdarma).
        private async Task<int> RunUntranslatedDuplicatesMigrationOnceAsync()
        {
            const string prefKey = "UntranslatedProductMergeDone_v1";
            if (Preferences.Default.Get(prefKey, false)) return 0;

            int count = await MergeUntranslatedDuplicatesAsync();
            Preferences.Default.Set(prefKey, true);
            return count;
        }

        // Opraví konkrétní known-bad DeepL překlady, které appka dřív mohla uložit (např.
        // "Oil" -> "Ropa" místo "Olej"). Musí běžet PŘED normálním sloučením duplicit níž -
        // jinak by "Olej"/"Olej" (nepřeložený pár) mohl při vlastním pokusu o překlad omylem
        // splynout se špatně pojmenovanou "Ropou" místo naopak.
        private async Task<int> ApplyKnownTranslationCorrectionsAsync()
        {
            var products = await _db.Table<LocalProduct>().ToListAsync();
            int fixedCount = 0;

            foreach (var product in products)
            {
                if (string.Equals(product.Name_CS, "Ropa", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(product.Name_EN, "Oil", StringComparison.OrdinalIgnoreCase))
                {
                    product.Name_CS = "Olej";
                    await _db.UpdateAsync(product);
                    fixedCount++;
                }
            }

            return fixedCount;
        }

        // Opraví velikost prvního písmene u produktů z doby před zavedením automatické
        // kapitalizace (nová jména se kapitalizují už při vytvoření).
        private async Task<int> NormalizeProductCapitalizationAsync()
        {
            var products = await _db.Table<LocalProduct>().ToListAsync();
            int fixedCount = 0;

            foreach (var product in products)
            {
                string properCs = Capitalize(product.Name_CS);
                string properEn = Capitalize(product.Name_EN);

                if (properCs != product.Name_CS || properEn != product.Name_EN)
                {
                    product.Name_CS = properCs;
                    product.Name_EN = properEn;
                    await _db.UpdateAsync(product);
                    fixedCount++;
                }
            }

            return fixedCount;
        }

        // FÁZE 1: bezpečné, offline sloučení - stejné slovo napříč Name_CS/Name_EN obou produktů
        // po normalizaci (diakritika/velikost/pomlčky nehrají roli). Žádné volání DeepL.
        private async Task<int> MergeByNormalizedNameOverlapAsync()
        {
            var products = await _db.Table<LocalProduct>().ToListAsync();
            var ingredientCounts = (await _db.Table<RecipeIngredient>().ToListAsync())
                .GroupBy(ri => ri.ProductId)
                .ToDictionary(g => g.Key, g => g.Count());

            var keys = products.Select(p => (
                Cs: TextNormalizationHelper.NormalizeForComparison(p.Name_CS),
                En: TextNormalizationHelper.NormalizeForComparison(p.Name_EN),
                Bases: GetParenthesizedBases(p))).ToList();

            int merged = 0;

            for (int i = 0; i < products.Count; i++)
            {
                var a = products[i];
                if (a.Id == 0) continue;

                for (int j = i + 1; j < products.Count; j++)
                {
                    var b = products[j];
                    if (b.Id == 0) continue;

                    var (csA, enA, basesA) = keys[i];
                    var (csB, enB, basesB) = keys[j];

                    bool sameWord =
                        (csA.Length > 0 && (csA == csB || csA == enB)) ||
                        (enA.Length > 0 && (enA == enB || enA == csB)) ||
                        basesA.Intersect(basesB).Any();

                    if (!sameWord) continue;

                    int usageA = ingredientCounts.GetValueOrDefault(a.Id, 0);
                    int usageB = ingredientCounts.GetValueOrDefault(b.Id, 0);

                    var (canonical, duplicate) = usageB > usageA ? (b, a) : (a, b);

                    await MergeProductPairAsync(canonical, duplicate);
                    duplicate.Id = 0;
                    merged++;

                    if (ReferenceEquals(duplicate, a)) break;
                }
            }

            return merged;
        }

        // FÁZE 2: produkty, které nikdy neměly opravdový překlad (Name_CS == Name_EN) - zkusí
        // přeložit a najít odpovídající existující produkt ve druhém jazyce. Respektuje DeepL
        // kvótu (TranslateAsync sama vrátí null při nedostatku) - bezpečně se dá spustit znovu.
        private async Task<int> MergeUntranslatedDuplicatesAsync()
        {
            var products = await _db.Table<LocalProduct>().ToListAsync();
            int merged = 0;

            var untranslated = products.Where(p => string.Equals(p.Name_CS, p.Name_EN, StringComparison.Ordinal)).ToList();

            foreach (var product in untranslated)
            {
                if (product.Id == 0) continue;

                string guessedLang = ContainsCzechDiacritics(product.Name_CS) ? "cs" : "en";
                string otherLang = guessedLang == "cs" ? "en" : "cs";

                string? translated = IngredientTranslationOverrides.TryGet(product.Name_CS, guessedLang)
                    ?? await TranslationService.TranslateAsync(product.Name_CS, otherLang, guessedLang);
                if (string.IsNullOrWhiteSpace(translated)) continue; if (string.IsNullOrWhiteSpace(translated)) continue;

                string normalizedTranslated = TextNormalizationHelper.NormalizeForComparison(translated);

                var refreshed = await _db.Table<LocalProduct>().ToListAsync();
                var match = refreshed.FirstOrDefault(p =>
                    p.Id != product.Id &&
                    (TextNormalizationHelper.NormalizeForComparison(p.Name_CS) == normalizedTranslated ||
                     TextNormalizationHelper.NormalizeForComparison(p.Name_EN) == normalizedTranslated));

                if (match != null)
                {
                    await MergeProductPairAsync(match, product);
                    merged++;
                }
                else
                {
                    if (guessedLang == "cs") product.Name_EN = Capitalize(translated);
                    else product.Name_CS = Capitalize(translated);
                    await _db.UpdateAsync(product);
                }
            }

            return merged;
        }

        private async Task MergeProductPairAsync(LocalProduct canonical, LocalProduct duplicate)
        {
            var ingredientsToMove = await _db.Table<RecipeIngredient>().Where(ri => ri.ProductId == duplicate.Id).ToListAsync();
            foreach (var ri in ingredientsToMove)
            {
                ri.ProductId = canonical.Id;
                await _db.UpdateAsync(ri);
            }

            var aliasesToMove = await _db.Table<LocalProductAlias>().Where(a => a.ProductId == duplicate.Id).ToListAsync();
            foreach (var duplicateAlias in aliasesToMove)
            {
                duplicateAlias.ProductId = canonical.Id;
                await _db.UpdateAsync(duplicateAlias);
            }

            if (!string.Equals(canonical.Name_CS, duplicate.Name_CS, StringComparison.Ordinal))
                await AddAliasIfMissingAsync(duplicate.Name_CS, canonical.Id);
            if (!string.Equals(canonical.Name_EN, duplicate.Name_EN, StringComparison.Ordinal))
                await AddAliasIfMissingAsync(duplicate.Name_EN, canonical.Id);

            if (duplicate.HasManualPrice && !canonical.HasManualPrice)
            {
                canonical.HasManualPrice = true;
                canonical.ManualPrice = duplicate.ManualPrice;
            }
            if (canonical.TypicalUnitWeightGrams <= 0 && duplicate.TypicalUnitWeightGrams > 0)
                canonical.TypicalUnitWeightGrams = duplicate.TypicalUnitWeightGrams;

            await _db.UpdateAsync(canonical);
            await _db.DeleteAsync(duplicate);
        }

        private async Task AddAliasIfMissingAsync(string aliasText, int productId)
        {
            string trimmed = aliasText.Trim();
            if (trimmed.Length == 0) return;

            var existingAliases = await _db.Table<LocalProductAlias>().Where(a => a.ProductId == productId).ToListAsync();
            bool exists = existingAliases.Any(a => TextNormalizationHelper.NormalizeForComparison(a.Alias) == TextNormalizationHelper.NormalizeForComparison(trimmed));
            if (!exists)
                await _db.InsertAsync(new LocalProductAlias { Alias = trimmed, ProductId = productId });
        }

        private static List<string> GetParenthesizedBases(LocalProduct p)
        {
            var result = new List<string>();

            var (baseCs, hasParenCs) = TextNormalizationHelper.SplitBaseAndParenthetical(p.Name_CS);
            if (hasParenCs && baseCs.Length > 0) result.Add(baseCs);

            var (baseEn, hasParenEn) = TextNormalizationHelper.SplitBaseAndParenthetical(p.Name_EN);
            if (hasParenEn && baseEn.Length > 0) result.Add(baseEn);

            return result;
        }

        private static bool ContainsCzechDiacritics(string text) =>
            text.Any(c => "áčďéěíňóřšťúůýžÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ".Contains(c));
    }
}