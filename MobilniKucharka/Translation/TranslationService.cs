using MobilniKucharka.Services;
using System.Text.Json;

namespace MobilniKucharka.Translation
{
    // Překlad receptů CS <-> EN přes DeepL API.
    public class TranslationService
    {
        private readonly HttpClient _httpClient = new();

        private static bool IsFreeApiKey =>
            !string.IsNullOrEmpty(Secrets.DeepLApiKey) && Secrets.DeepLApiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase);

        private static string Endpoint =>
            IsFreeApiKey ? "https://api-free.deepl.com/v2/translate" : "https://api.deepl.com/v2/translate";

        private static string ToDeepLTargetCode(string appLangCode) =>
            appLangCode.Equals("en", StringComparison.OrdinalIgnoreCase) ? "EN-US" : "CS";

        private static string ToDeepLSourceCode(string appLangCode) =>
            appLangCode.Equals("en", StringComparison.OrdinalIgnoreCase) ? "EN" : "CS";

        public async Task<List<string>?> TranslateBatchAsync(List<string> texts, string targetAppLang, string? sourceAppLang = null)
        {
            if (texts == null || texts.Count == 0) return [];
            if (string.IsNullOrWhiteSpace(Secrets.DeepLApiKey))
            {
                System.Diagnostics.Debug.WriteLine("[DeepL] Chybí Secrets.DeepLApiKey - překlad se nespustil.");
                return null;
            }

            try
            {
                var form = new List<KeyValuePair<string, string>>
                {
                    new("target_lang", ToDeepLTargetCode(targetAppLang))
                };

                if (!string.IsNullOrWhiteSpace(sourceAppLang))
                    form.Add(new("source_lang", ToDeepLSourceCode(sourceAppLang)));

                foreach (var t in texts)
                    form.Add(new("text", t ?? string.Empty));

                using var content = new FormUrlEncodedContent(form);
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
                // DeepL očekává klíč v hlavičce, ne jako "auth_key" v těle requestu - tohle je jediná
                // podporovaná metoda pro nové účty. Bez ní appka dostane 403 s "Missing Authorization header".
                request.Headers.Add("Authorization", $"DeepL-Auth-Key {Secrets.DeepLApiKey}");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[DeepL] Chyba {(int)response.StatusCode} {response.StatusCode}: {errorBody}");
                    return null;
                }

                var contentString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(contentString) || !contentString.TrimStart().StartsWith('{'))
                {
                    System.Diagnostics.Debug.WriteLine("[DeepL] Odpověď nevypadá jako platný JSON.");
                    return null;
                }

                var root = JsonSerializer.Deserialize<JsonElement>(contentString);
                if (!root.TryGetProperty("translations", out var translationsArray))
                    return null;

                var results = new List<string>();
                foreach (var t in translationsArray.EnumerateArray())
                    results.Add(t.GetProperty("text").GetString() ?? string.Empty);

                return results;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeepL] Výjimka při volání API: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> TranslateAsync(string text, string targetAppLang, string? sourceAppLang = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var result = await TranslateBatchAsync([text], targetAppLang, sourceAppLang);
            return result?.FirstOrDefault();
        }

        public async Task<bool> TranslateRecipeNameAndStepsAsync(MobilniKucharka.Classes.Recipe.Recipe recipe, string fromLang, string toLang, bool skipName = false)
        {
            bool fromCs = fromLang.Equals("cs", StringComparison.OrdinalIgnoreCase);
            var sourceSteps = fromCs ? recipe.Steps_CS : recipe.Steps_EN;

            var batch = new List<string>();
            if (!skipName)
                batch.Add(fromCs ? recipe.Name_CS : recipe.Name_EN);
            batch.AddRange(sourceSteps);

            if (batch.Count == 0) return true; // jméno přeskočeno a žádné kroky k překladu - není chyba

            var translated = await TranslateBatchAsync(batch, toLang, fromLang);
            if (translated == null || translated.Count != batch.Count) return false;

            int stepsStartIndex = skipName ? 0 : 1;
            var translatedSteps = translated.Skip(stepsStartIndex).ToList();

            if (fromCs)
            {
                if (!skipName) recipe.Name_EN = translated[0];
                recipe.Steps_EN = translatedSteps;
            }
            else
            {
                if (!skipName) recipe.Name_CS = translated[0];
                recipe.Steps_CS = translatedSteps;
            }

            return true;
        }
    }
}