using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MobilniKucharka.Services
{
    public enum DiscussionPostResult
    {
        Success,
        NotConfigured,
        NetworkOrApiFailure
    }

    public class GitHubDiscussionService
    {
        private readonly HttpClient _httpClient = new();
        private const string RepoOwner = "OndyMikula";
        private const string RepoName = "MobilniKucharka";
        private const int DiscussionNumber = 56;
        private const string GraphQlUrl = "https://api.github.com/graphql";

        // Vrací rozlišený výsledek místo prostého bool - "token není nastavený" (lokální build bez
        // reálného Secrets.cs, nebo appka postavená před přidáním téhle funkce) je úplně jiná
        // situace než "GitHub API/síť selhaly", ale dřív obě vracely stejné false a appka to pak
        // uživateli ukazovala jako "zkontroluj internetové připojení", i když o připojení vůbec
        // nešlo. Debug.WriteLine na každém kroku - stejný vzor jako ImageHelper.ResolveImageSrc -
        // ať se příště dá skutečná příčina dohledat přes adb log/VS Debug Output, ne odhadovat.
        public async Task<DiscussionPostResult> PostBugReportAsync(string body)
        {
            if (string.IsNullOrWhiteSpace(Secrets.GitHubDiscussionToken) ||
                Secrets.GitHubDiscussionToken.Contains("paste_your", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("[GitHubDiscussionService] GitHubDiscussionToken není nastavený (placeholder nebo prázdný) - lokální Secrets.cs nejspíš neobsahuje reálný token.");
                return DiscussionPostResult.NotConfigured;
            }

            try
            {
                string? discussionId = await GetDiscussionIdAsync();
                if (string.IsNullOrWhiteSpace(discussionId))
                {
                    Debug.WriteLine("[GitHubDiscussionService] Nepodařilo se získat ID diskuze - viz předchozí log řádek s detailem chyby.");
                    return DiscussionPostResult.NetworkOrApiFailure;
                }

                bool success = await AddDiscussionCommentAsync(discussionId, body);
                return success ? DiscussionPostResult.Success : DiscussionPostResult.NetworkOrApiFailure;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GitHubDiscussionService] Výjimka při odesílání: {ex.Message}");
                return DiscussionPostResult.NetworkOrApiFailure;
            }
        }

        private async Task<string?> GetDiscussionIdAsync()
        {
            var payload = new
            {
                query = "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){discussion(number:$number){id}}}",
                variables = new { owner = RepoOwner, name = RepoName, number = DiscussionNumber }
            };

            string? responseJson = await SendGraphQlRequestAsync(payload);
            if (responseJson == null) return null;

            try
            {
                var root = JsonSerializer.Deserialize<JsonElement>(responseJson);
                if (root.TryGetProperty("errors", out var errors))
                {
                    Debug.WriteLine($"[GitHubDiscussionService] GraphQL chyba (getDiscussionId): {errors}");
                    return null;
                }

                return root.GetProperty("data").GetProperty("repository").GetProperty("discussion").GetProperty("id").GetString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GitHubDiscussionService] Nepodařilo se rozparsovat odpověď (getDiscussionId): {ex.Message}\nOdpověď: {responseJson}");
                return null;
            }
        }

        private async Task<bool> AddDiscussionCommentAsync(string discussionId, string body)
        {
            var payload = new
            {
                query = "mutation($discussionId:ID!,$body:String!){addDiscussionComment(input:{discussionId:$discussionId,body:$body}){comment{id}}}",
                variables = new { discussionId, body }
            };

            string? responseJson = await SendGraphQlRequestAsync(payload);
            if (responseJson == null) return false;

            try
            {
                var root = JsonSerializer.Deserialize<JsonElement>(responseJson);
                if (root.TryGetProperty("errors", out var errors))
                {
                    Debug.WriteLine($"[GitHubDiscussionService] GraphQL chyba (addDiscussionComment): {errors}");
                    return false;
                }

                return root.GetProperty("data").GetProperty("addDiscussionComment").GetProperty("comment").TryGetProperty("id", out _);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GitHubDiscussionService] Nepodařilo se rozparsovat odpověď (addDiscussionComment): {ex.Message}\nOdpověď: {responseJson}");
                return false;
            }
        }

        private async Task<string?> SendGraphQlRequestAsync(object payload)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {Secrets.GitHubDiscussionToken}");
            request.Headers.Add("User-Agent", "MobilniKucharka-App");

            var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[GitHubDiscussionService] HTTP {(int)response.StatusCode} {response.StatusCode}: {responseBody}");
                return null;
            }

            return responseBody;
        }
    }
}