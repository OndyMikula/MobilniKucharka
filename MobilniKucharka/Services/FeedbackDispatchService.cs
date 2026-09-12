using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MobilniKucharka.Services
{
    public enum FeedbackDispatchResult
    {
        Success,
        NotConfigured,
        NetworkOrApiFailure
    }

    // Odesílá hlášení chyb a nápady na vylepšení tiše na pozadí přes GitHub repository_dispatch,
    // který spustí .github/workflows/send-feedback-email.yml (v samostatném repozitáři
    // MobilniKucharka-Feedback - NE tady, v hlavním kódu appky). Repository_dispatch endpoint
    // vyžaduje token se scope "Contents: Read and write" - mnohem širší oprávnění, než mělo dřívější
    // GitHub Discussions řešení (jen "Discussions: write"), takže token míří na prázdný, oddělený
    // repozitář (stejný princip jako MobilniKucharka-SharedRecipes u RecipeLinkShareService) - i po
    // vytažení z APK by šlo poškodit nanejvýš tenhle prázdný repozitář, nikdy skutečný zdrojový kód.
    public class FeedbackDispatchService
    {
        // Sdílená statická instance, ne nová při každém vytvoření služby - viz Copilot review
        // (opakované vytváření HttpClient může vést k vyčerpání socketů/DNS cache).
        private static readonly HttpClient _httpClient = new();
        private static readonly string DispatchUrl = "https://api.github.com/repos/OndyMikula/MobilniKucharka-Feedback/dispatches";

        public async Task<FeedbackDispatchResult> SendFeedbackAsync(string type, string title, string description, string reproSteps, string appVersion, string language, string userEmail)
        {
            if (string.IsNullOrWhiteSpace(Secrets.FeedbackDispatchToken) ||
                Secrets.FeedbackDispatchToken.Contains("paste_your", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("[FeedbackDispatchService] FeedbackDispatchToken není nastavený.");
                return FeedbackDispatchResult.NotConfigured;
            }

            try
            {
                var payload = new
                {
                    event_type = "feedback",
                    client_payload = new { type, title, description, reproSteps, appVersion, language, userEmail }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, DispatchUrl)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/vnd.github+json")
                };
                request.Headers.Add("Authorization", $"Bearer {Secrets.FeedbackDispatchToken}");
                request.Headers.Add("User-Agent", "MobilniKucharka-App");
                request.Headers.Add("Accept", "application/vnd.github+json");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode) return FeedbackDispatchResult.Success;

                string responseBody = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[FeedbackDispatchService] HTTP {(int)response.StatusCode}: {responseBody}");
                return FeedbackDispatchResult.NetworkOrApiFailure;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FeedbackDispatchService] Výjimka: {ex.Message}");
                return FeedbackDispatchResult.NetworkOrApiFailure;
            }
        }
    }
}