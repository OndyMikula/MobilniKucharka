using MobilniKucharka.Services;
using System.Text;
using System.Text.Json;

namespace MobilniKucharka.Classes.Recipe.Sharing
{
    public static class RecipeLinkShareService
    {
        private const string RepoOwner = "OndyMikula";
        private const string RepoName = "MobilniKucharka-SharedRecipes";
        private const string BranchName = "main";
        private static readonly HttpClient _httpClient = new();

        public static async Task<string?> ShareViaLinkAsync(Recipe recipe)
        {
            try
            {
                await CleanupExpiredLinksAsync();

                string? photoBase64 = null;
                if (!string.IsNullOrWhiteSpace(recipe.ImageUrl) && File.Exists(recipe.ImageUrl))
                {
                    byte[] bytes = await File.ReadAllBytesAsync(recipe.ImageUrl);
                    photoBase64 = Convert.ToBase64String(bytes);
                }

                var shared = new SharedRecipeLinkData
                {
                    Name_CS = recipe.Name_CS,
                    Name_EN = recipe.Name_EN,
                    DescriptionText = recipe.DescriptionText,
                    IngredientsRaw = recipe.IngredientsRaw,
                    StepsJson_CS = recipe.StepsJson_CS,
                    StepsJson_EN = recipe.StepsJson_EN,
                    EquipmentJson = recipe.EquipmentJson,
                    DietaryFlagsJson = recipe.DietaryFlagsJson,
                    Protein = recipe.Protein,
                    Carbs = recipe.Carbs,
                    Fat = recipe.Fat,
                    Sugar = recipe.Sugar,
                    IsNutritionEstimated = recipe.IsNutritionEstimated,
                    ManualCost = recipe.ManualCost,
                    PrepTime = recipe.PrepTime,
                    ServingSize = recipe.ServingSize,
                    PhotoBase64 = photoBase64,
                    ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
                };

                string guid = Guid.NewGuid().ToString("N");
                string json = JsonSerializer.Serialize(shared);
                string contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

                var payload = new { message = $"Sdílený recept {guid}", content = contentBase64, branch = BranchName };

                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/recipes/{guid}.json";
                var request = new HttpRequestMessage(HttpMethod.Put, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {Secrets.GitHubShareToken}");
                request.Headers.Add("User-Agent", "MobilniKucharka-App");
                request.Headers.Add("Accept", "application/vnd.github+json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                return $"https://ondymikula.github.io/recipe.html?id={guid}";
            }
            catch
            {
                return null;
            }
        }

        public static async Task<Recipe?> ImportFromLinkAsync(string guid)
        {
            try
            {
                string rawUrl = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/{BranchName}/recipes/{guid}.json";
                var getResponse = await _httpClient.GetAsync(rawUrl);
                if (!getResponse.IsSuccessStatusCode) return null;

                string json = await getResponse.Content.ReadAsStringAsync();
                var shared = JsonSerializer.Deserialize<SharedRecipeLinkData>(json);
                if (shared == null) return null;

                if (shared.ExpiresAtUtc < DateTime.UtcNow)
                {
                    await DeleteSharedFileAsync(guid);
                    return null;
                }

                string? photoPath = null;
                if (!string.IsNullOrWhiteSpace(shared.PhotoBase64))
                {
                    byte[] bytes = Convert.FromBase64String(shared.PhotoBase64);
                    photoPath = Path.Combine(FileSystem.AppDataDirectory, $"{Guid.NewGuid()}_odkaz_recept.jpg");
                    await File.WriteAllBytesAsync(photoPath, bytes);
                }

                var recipe = new Recipe
                {
                    Name_CS = shared.Name_CS,
                    Name_EN = shared.Name_EN,
                    DescriptionText = shared.DescriptionText,
                    IngredientsRaw = shared.IngredientsRaw,
                    StepsJson_CS = shared.StepsJson_CS,
                    StepsJson_EN = shared.StepsJson_EN,
                    EquipmentJson = shared.EquipmentJson,
                    DietaryFlagsJson = shared.DietaryFlagsJson,
                    Protein = shared.Protein,
                    Carbs = shared.Carbs,
                    Fat = shared.Fat,
                    Sugar = shared.Sugar,
                    IsNutritionEstimated = shared.IsNutritionEstimated,
                    ManualCost = shared.ManualCost,
                    PrepTime = shared.PrepTime,
                    ServingSize = shared.ServingSize,
                    Category = "Vytvořené recepty",
                    ImageUrl = photoPath ?? ""
                };

                await DeleteSharedFileAsync(guid);

                return recipe;
            }
            catch
            {
                return null;
            }
        }

        private static async Task DeleteSharedFileAsync(string guid)
        {
            try
            {
                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/recipes/{guid}.json";

                var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.Add("Authorization", $"Bearer {Secrets.GitHubShareToken}");
                getRequest.Headers.Add("User-Agent", "MobilniKucharka-App");
                getRequest.Headers.Add("Accept", "application/vnd.github+json");

                var getResponse = await _httpClient.SendAsync(getRequest);
                if (!getResponse.IsSuccessStatusCode) return;

                string metaJson = await getResponse.Content.ReadAsStringAsync();
                var meta = JsonSerializer.Deserialize<JsonElement>(metaJson);
                string sha = meta.GetProperty("sha").GetString() ?? "";

                var deletePayload = new { message = $"Smazat recept {guid}", sha, branch = BranchName };

                var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(deletePayload), Encoding.UTF8, "application/json")
                };
                deleteRequest.Headers.Add("Authorization", $"Bearer {Secrets.GitHubShareToken}");
                deleteRequest.Headers.Add("User-Agent", "MobilniKucharka-App");
                deleteRequest.Headers.Add("Accept", "application/vnd.github+json");

                await _httpClient.SendAsync(deleteRequest);
            }
            catch
            {
                // smazání se nezdařilo -> zůstane na příští úklid podle expirace
            }
        }

        private static async Task CleanupExpiredLinksAsync()
        {
            try
            {
                string listUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/recipes";
                var listRequest = new HttpRequestMessage(HttpMethod.Get, listUrl);
                listRequest.Headers.Add("Authorization", $"Bearer {Secrets.GitHubShareToken}");
                listRequest.Headers.Add("User-Agent", "MobilniKucharka-App");
                listRequest.Headers.Add("Accept", "application/vnd.github+json");

                var listResponse = await _httpClient.SendAsync(listRequest);
                if (!listResponse.IsSuccessStatusCode) return;

                string listJson = await listResponse.Content.ReadAsStringAsync();
                var files = JsonSerializer.Deserialize<List<JsonElement>>(listJson) ?? [];

                foreach (var file in files)
                {
                    string name = file.GetProperty("name").GetString() ?? "";
                    if (!name.EndsWith(".json")) continue;

                    string guid = name.Replace(".json", "");
                    string rawUrl = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/{BranchName}/recipes/{name}";

                    var contentResponse = await _httpClient.GetAsync(rawUrl);
                    if (!contentResponse.IsSuccessStatusCode) continue;

                    string json = await contentResponse.Content.ReadAsStringAsync();
                    var shared = JsonSerializer.Deserialize<SharedRecipeLinkData>(json);

                    if (shared != null && shared.ExpiresAtUtc < DateTime.UtcNow)
                        await DeleteSharedFileAsync(guid);
                }
            }
            catch
            {
                // úklid se nezdařil -> zkusí se při dalším sdílení
            }
        }
    }
}
