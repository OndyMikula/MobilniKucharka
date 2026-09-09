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

    // Obecná služba pro psaní komentářů do libovolné existující GitHub Discussion přes GraphQL -
    // dřív specifická jen pro hlášení chyb (Discussion #56), teď sdílená i s nápady na vylepšení
    // (Discussion #71), viz BugReportPage.razor / IdeaPage.razor.
    public class GitHubDiscussionService
    {
        private readonly HttpClient _httpClient = new();
        private const string RepoOwner = "OndyMikula";
        private const string RepoName = "MobilniKucharka";
        private const string GraphQlUrl = "https://api.github.com/graphql";

        public const int BugReportDiscussionNumber = 56;
        public const int IdeaDiscussionNumber = 71;

        public async Task<DiscussionPostResult> PostCommentAsync(int discussionNumber, string body)
        {
            if (string.IsNullOrWhiteSpace(Secrets.GitHubDiscussionToken) ||
                Secrets.GitHubDiscussionToken.Contains("paste_your", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("[GitHubDiscussionService] GitHubDiscussionToken není nastavený (placeholder nebo prázdný) - lokální Secrets.cs nejspíš neobsahuje reálný token.");
                return DiscussionPostResult.NotConfigured;
            }

            try
            {
                string? discussionId = await GetDiscussionIdAsync(discussionNumber);
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

        private async Task<string?> GetDiscussionIdAsync(int discussionNumber)
        {
            var payload = new
            {
                query = "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){discussion(number:$number){id}}}",
                variables = new { owner = RepoOwner, name = RepoName, number = discussionNumber }
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