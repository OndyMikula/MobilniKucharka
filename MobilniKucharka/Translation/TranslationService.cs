using MobilniKucharka.Services;
using System.Text.Json;

namespace MobilniKucharka.Translation
{
    // Překlad receptů CS <-> EN přes DeepL API.
    public static class TranslationService
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

        private static DateTime _quotaCheckedAtUtc = DateTime.MinValue;
        private static bool _quotaExceeded;
        private static readonly TimeSpan QuotaCheckInterval = TimeSpan.FromHours(6);

        private static bool IsFreeApiKey =>
            !string.IsNullOrEmpty(Secrets.DeepLApiKey) && Secrets.DeepLApiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase);

        private static string Endpoint =>
            IsFreeApiKey ? "https://api-free.deepl.com/v2/translate" : "https://api.deepl.com/v2/translate";

        private static string UsageEndpoint =>
            IsFreeApiKey ? "https://api-free.deepl.com/v2/usage" : "https://api.deepl.com/v2/usage";

        private static string ToDeepLTargetCode(string appLangCode) =>
            appLangCode.Equals("en", StringComparison.OrdinalIgnoreCase) ? "EN-US" : "CS";

        private static string ToDeepLSourceCode(string appLangCode) =>
            appLangCode.Equals("en", StringComparison.OrdinalIgnoreCase) ? "EN" : "CS";

        // /v2/usage se nepočítá do kvóty - bezpečné volat před každým překladem. Cachuje se na 6
        // hodin. Developer/Pro tarif má CELOŽIVOTNÍ (neobnovující se) limit 1 000 000 znaků.
        private static async Task<bool> HasQuotaAvailableAsync()
        {
            if (DateTime.UtcNow - _quotaCheckedAtUtc < QuotaCheckInterval)
                return !_quotaExceeded;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
                request.Headers.Add("Authorization", $"DeepL-Auth-Key {Secrets.DeepLApiKey}");

                var response = await _httpClient.SendAsync(request);
                _quotaCheckedAtUtc = DateTime.UtcNow;

                if (!response.IsSuccessStatusCode) return true;

                string json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                long used = data.TryGetProperty("character_count", out var usedProp) ? usedProp.GetInt64() : 0;
                long limit = data.TryGetProperty("character_limit", out var limitProp) ? limitProp.GetInt64() : long.MaxValue;

                _quotaExceeded = limit > 0 && used >= limit - 2000;

                if (_quotaExceeded)
                    System.Diagnostics.Debug.WriteLine($"[DeepL] Kvóta téměř vyčerpána: {used}/{limit} znaků.");

                return !_quotaExceeded;
            }
            catch
            {
                _quotaCheckedAtUtc = DateTime.UtcNow;
                return true;
            }
        }

        public static async Task<List<string>?> TranslateBatchAsync(List<string> texts, string targetAppLang, string? sourceAppLang = null)
        {
            if (texts == null || texts.Count == 0) return [];
            if (string.IsNullOrWhiteSpace(Secrets.DeepLApiKey))
            {
                System.Diagnostics.Debug.WriteLine("[DeepL] Chybí Secrets.DeepLApiKey - překlad se nespustil.");
                return null;
            }

            if (!await HasQuotaAvailableAsync())
            {
                System.Diagnostics.Debug.WriteLine("[DeepL] Překlad přeskočen - kvóta téměř vyčerpaná.");
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

        public static async Task<string?> TranslateAsync(string text, string targetAppLang, string? sourceAppLang = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var result = await TranslateBatchAsync([text], targetAppLang, sourceAppLang);
            return result?.FirstOrDefault();
        }

        public static async Task<bool> TranslateRecipeNameAndStepsAsync(MobilniKucharka.Classes.Recipe.Recipe recipe, string fromLang, string toLang, bool skipName = false)
        {
            bool fromCs = fromLang.Equals("cs", StringComparison.OrdinalIgnoreCase);
            var sourceSteps = fromCs ? recipe.Steps_CS : recipe.Steps_EN;

            var batch = new List<string>();
            if (!skipName)
                batch.Add(fromCs ? recipe.Name_CS : recipe.Name_EN);
            batch.AddRange(sourceSteps);

            if (batch.Count == 0) return true;

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