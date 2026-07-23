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
            // 1. Nejprve zkusíme najít recept v naší lokální mezipaměti
            var cached = await _db.Table<Recipe>()
                                 .Where(r => r.ExternalSourceId == $"spoon_{spoonacularId}")
                                 .FirstOrDefaultAsync();

            if (cached != null)
            {
                return cached; // Vrátíme lokální kopii (0 spotřebovaných API bodů!)
            }

            // 2. Pokud v cache není, stáhneme ho z API
            string url = $"https://api.spoonacular.com/recipes/{spoonacularId}/information?apiKey={ApiKey}&includeNutrition=true";
            try
            {
                var response = await _httpClient.GetAsync(url);

                // 1. Zkontrolujeme, jestli API vůbec odpovědělo kladně (jinak nám třeba došly API kredity)
                if (!response.IsSuccessStatusCode)
                {
                    // Můžeš si sem dát i Debug.WriteLine, ať víš, že došly kredity
                    return null; // Aplikace nespadne, jen se prostě nic nenačte
                }

                // 2. Pro jistotu si to načteme nejdřív jako obyčejný text
                var contentString = await response.Content.ReadAsStringAsync();

                // 3. Pokud je odpověď úplně prázdná, vykašleme se na to
                if (string.IsNullOrWhiteSpace(contentString))
                {
                    return null;
                }

                // 4. Až teď to bezpečně převedeme na JSON
                var data = JsonSerializer.Deserialize<JsonElement>(contentString);

                // 3. Mapování z JSON na náš objekt Recipe
                var recipe = new Recipe
                {
                    ExternalSourceId = $"spoon_{spoonacularId}",
                    Name_CS = data.GetProperty("title").GetString() ?? "",
                    Name_EN = data.GetProperty("title").GetString() ?? "",
                    PrepTime = data.GetProperty("readyInMinutes").GetInt32(),
                    ImageUrl = data.GetProperty("image").GetString() ?? "",

                    // Nutriční hodnoty vytažené z analýzy Spoonacularu
                    Protein = ExtractNutrient(data, "Protein"),
                    Carbs = ExtractNutrient(data, "Carbohydrates"),
                    Fat = ExtractNutrient(data, "Fat"),
                    Sugar = ExtractNutrient(data, "Sugar"),

                    StepsJson = ExtractSteps(data),
                    EquipmentJson = "[]",
                    DietaryFlagsJson = ExtractDiets(data)
                };

                // 4. Uložíme do naší lokální DB pro příště
                await _db.InsertAsync(recipe);
                return recipe;
            }
            catch (JsonException)
            {
                // Tento catch chytne přesně tu tvoji chybu, kdyby to náhodou nebyl validní JSON
                // Aplikace poběží dál.
                return null;
            }
            catch (Exception)
            {
                // Tento catch chytne všechny ostatní průšvihy (např. úplně vypadlý internet)
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