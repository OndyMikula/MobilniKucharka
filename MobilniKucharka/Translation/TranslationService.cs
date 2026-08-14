using System.Text.Json;
using MobilniKucharka.Services;

namespace MobilniKucharka.Translation
{
    // Překlad receptů CS <-> EN přes DeepL API.
    // Klíč čti ze Secrets.DeepLApiKey (generováno CI z GitHub Actions secretu DEEPL_API_KEY).
    public class TranslationService
    {
        private readonly HttpClient _httpClient = new();

        // Klíče pro free-tier účet DeepL vždy končí ":fx" a musí jít na api-free endpoint.
        // Developer/Pro klíč ":fx" nemá, jde na plný api.deepl.com endpoint.
        private static bool IsFreeApiKey =>
            !string.IsNullOrEmpty(Secrets.DeepLApiKey) && Secrets.DeepLApiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase);

        private static string Endpoint =>
            IsFreeApiKey ? "https://api-free.deepl.com/v2/translate" : "https://api.deepl.com/v2/translate";

        // Appka pracuje s "cs"/"en" (stejně jako AppLanguageCode). DeepL chce jako target "CS" / "EN-US", jako source stačí "EN".
        private static string ToDeepLTargetCode(string appLangCode) =>
            appLangCode.Equals("en", StringComparison.OrdinalIgnoreCase) ? "EN-US" : "CS";

        private static string ToDeepLSourceCode(string appLangCode) =>
            appLangCode.Equals("en", StringComparison.OrdinalIgnoreCase) ? "EN" : "CS";

        // Přeloží dávku textů najednou (šetří API volání i znakový limit oproti jednotlivým requestům).
        // Vrací null při chybě/chybějícím klíči - volající by na to měl reagovat, ne tiše selhat.
        public async Task<List<string>?> TranslateBatchAsync(List<string> texts, string targetAppLang, string? sourceAppLang = null)
        {
            if (texts == null || texts.Count == 0) return [];
            if (string.IsNullOrWhiteSpace(Secrets.DeepLApiKey)) return null;

            try
            {
                var form = new List<KeyValuePair<string, string>>
                {
                    new("auth_key", Secrets.DeepLApiKey),
                    new("target_lang", ToDeepLTargetCode(targetAppLang))
                };

                if (!string.IsNullOrWhiteSpace(sourceAppLang))
                    form.Add(new("source_lang", ToDeepLSourceCode(sourceAppLang)));

                foreach (var t in texts)
                    form.Add(new("text", t ?? string.Empty));

                using var content = new FormUrlEncodedContent(form);
                var response = await _httpClient.PostAsync(Endpoint, content).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode) return null;

                var contentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(contentString) || !contentString.TrimStart().StartsWith('{'))
                    return null;

                var root = JsonSerializer.Deserialize<JsonElement>(contentString);
                if (!root.TryGetProperty("translations", out var translationsArray))
                    return null;

                var results = new List<string>();
                foreach (var t in translationsArray.EnumerateArray())
                    results.Add(t.GetProperty("text").GetString() ?? string.Empty);

                return results;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> TranslateAsync(string text, string targetAppLang, string? sourceAppLang = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var result = await TranslateBatchAsync([text], targetAppLang, sourceAppLang).ConfigureAwait(false);
            return result?.FirstOrDefault();
        }

        // Přeloží JEN Name a Steps - mají vlastní _CS/_EN sloupce na Recipe, takže žádná cache navíc netřeba,
        // jakmile se jednou přeloží, zůstává to tam napořád. DescriptionText/IngredientsRaw řeší
        // BudgetPlannerService.TranslateAndSaveRecipeAsync přes RecipeTranslationCache, ne tahle metoda.
        public async Task<bool> TranslateRecipeNameAndStepsAsync(Classes.Recipe.Recipe recipe, string fromLang, string toLang)
        {
            bool fromCs = fromLang.Equals("cs", StringComparison.OrdinalIgnoreCase);

            string sourceName = fromCs ? recipe.Name_CS : recipe.Name_EN;
            var sourceSteps = fromCs ? recipe.Steps_CS : recipe.Steps_EN;

            var batch = new List<string> { sourceName };
            batch.AddRange(sourceSteps);

            var translated = await TranslateBatchAsync(batch, toLang, fromLang).ConfigureAwait(false);
            if (translated == null || translated.Count != batch.Count) return false;

            string translatedName = translated[0];
            var translatedSteps = translated.Skip(1).ToList();

            if (fromCs)
            {
                recipe.Name_EN = translatedName;
                recipe.Steps_EN = translatedSteps;
            }
            else
            {
                recipe.Name_CS = translatedName;
                recipe.Steps_CS = translatedSteps;
            }

            return true;
        }
    }
}