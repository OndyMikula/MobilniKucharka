using System.Text;
using System.Text.Json;

namespace MobilniKucharka.Services
{
    // Pošle hlášení chyby jako komentář do existující GitHub Discussion (#56,
    // OndyMikula/MobilniKucharka) přes GraphQL API. Token (Secrets.GitHubDiscussionToken) je
    // fine-grained PAT omezený jen na tenhle repozitář

    public class GitHubDiscussionService
    {
        private readonly HttpClient _httpClient = new();
        private const string RepoOwner = "OndyMikula";
        private const string RepoName = "MobilniKucharka";
        private const int DiscussionNumber = 56;
        private const string GraphQlUrl = "https://api.github.com/graphql";

        public async Task<bool> PostBugReportAsync(string body)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Secrets.GitHubDiscussionToken) ||
                    Secrets.GitHubDiscussionToken.Contains("paste_your", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string? discussionId = await GetDiscussionIdAsync();
                if (string.IsNullOrWhiteSpace(discussionId)) return false;

                return await AddDiscussionCommentAsync(discussionId, body);
            }
            catch
            {
                return false;
            }
        }

        // Discussion #56 už existuje (založená ručně na GitHubu) - appka si jen jednou za request
        // dotáhne její interní GraphQL "node id" (jiné číslo než viditelné číslo #56 v URL),
        // které mutace addDiscussionComment níže vyžaduje jako vstup.
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
                if (root.TryGetProperty("errors", out _)) return null;

                return root.GetProperty("data").GetProperty("repository").GetProperty("discussion").GetProperty("id").GetString();
            }
            catch
            {
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
                if (root.TryGetProperty("errors", out _)) return false;

                return root.GetProperty("data").GetProperty("addDiscussionComment").GetProperty("comment").TryGetProperty("id", out _);
            }
            catch
            {
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
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadAsStringAsync();
        }
    }
}