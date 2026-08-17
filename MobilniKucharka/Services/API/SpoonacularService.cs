using MobilniKucharka.Classes;
using MobilniKucharka.Classes.Recipe;
using SQLite;
using System.Net.Http.Json;
using System.Text.Json;

namespace MobilniKucharka.Services.Api
{
    //spoonacular - "stahovat si recepty ze Spoonacularu
    public class SpoonacularService(string dbPath)
    {
        private readonly HttpClient _httpClient = new();
        private readonly SQLiteAsyncConnection _db = new(dbPath);
        private static readonly string ApiKey = Secrets.SpoonacularApiKey;

        public async Task<Recipe?> GetRecipeWithCacheAsync(int spoonacularId)
        {
            var cached = await _db.Table<Recipe>()
                                 .Where(r => r.ExternalSourceId == $"spoon_{spoonacularId}")
                                 .FirstOrDefaultAsync();

            if (cached != null)
            {
                return cached;
            }

            string url = $"https://api.spoonacular.com/recipes/{spoonacularId}/information?apiKey={ApiKey}&includeNutrition=true";
            try
            {
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var contentString = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(contentString))
                {
                    return null;
                }

                var data = JsonSerializer.Deserialize<JsonElement>(contentString);

                // Zdroj je anglický - Name_CS/StepsJson_CS necháváme prázdné. RecipeDetailPage.OnAppearing
                // (přes EnsureRecipeLanguageAsync) pak automaticky doplní češtinu při prvním zobrazení.
                // Dřív se sem omylem plnilo Name_CS stejným textem jako Name_EN, takže se "už přeložený"
                // recept nikdy doopravdy nepřeložil.
                var recipe = new Recipe
                {
                    ExternalSourceId = $"spoon_{spoonacularId}",
                    Name_EN = data.GetProperty("title").GetString() ?? "",
                    PrepTime = data.GetProperty("readyInMinutes").GetInt32(),
                    ImageUrl = data.GetProperty("image").GetString() ?? "",
                    SourceUrl = data.TryGetProperty("sourceUrl", out var srcProp) ? srcProp.GetString() ?? "" : "",

                    Protein = ExtractNutrient(data, "Protein"),
                    Carbs = ExtractNutrient(data, "Carbohydrates"),
                    Fat = ExtractNutrient(data, "Fat"),
                    Sugar = ExtractNutrient(data, "Sugar"),

                    // Dřív omylem StepsJson (mrtvé pole, Steps_EN by bylo prázdné) - teď StepsJson_EN.
                    StepsJson_EN = ExtractSteps(data),
                    ServingSize = data.TryGetProperty("servings", out var servingsProp) && servingsProp.GetInt32() > 0
                        ? servingsProp.GetInt32()
                        : 1,
                    EquipmentJson = "[]",
                    DietaryFlagsJson = ExtractDiets(data)
                };

                await _db.InsertAsync(recipe);
                return recipe;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static double ExtractNutrient(JsonElement root, string nutrientName)
        {
            try
            {
                var nutrients = root.GetProperty("nutrition").GetProperty("nutrients");
                foreach (var n in nutrients.EnumerateArray())
                {
                    if (n.GetProperty("name").GetString() == nutrientName)
                        return n.GetProperty("amount").GetDouble();
                }
            }
            catch { }
            return 0;
        }

        private static string ExtractSteps(JsonElement root)
        {
            var stepsList = new List<string>();
            try
            {
                var analyzedInstructions = root.GetProperty("analyzedInstructions");
                if (analyzedInstructions.GetArrayLength() > 0)
                {
                    var steps = analyzedInstructions[0].GetProperty("steps");
                    foreach (var s in steps.EnumerateArray())
                    {
                        stepsList.Add(s.GetProperty("step").GetString() ?? "");
                    }
                }
            }
            catch { }
            return JsonSerializer.Serialize(stepsList);
        }

        private static string ExtractDiets(JsonElement root)
        {
            var dietsList = new List<string>();
            try
            {
                if (root.GetProperty("vegetarian").GetBoolean()) dietsList.Add("Vegetarian");
                if (root.GetProperty("vegan").GetBoolean()) dietsList.Add("Vegan");
                if (root.GetProperty("glutenFree").GetBoolean()) dietsList.Add("GlutenFree");
                if (root.GetProperty("dairyFree").GetBoolean()) dietsList.Add("DairyFree");
            }
            catch { }
            return JsonSerializer.Serialize(dietsList);
        }
    }
}