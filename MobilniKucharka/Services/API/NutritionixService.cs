using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MobilniKucharka.Services.Api
{
    //Nutritionix API "Bezplatný vývojářský tarif (Development Tier)" nabízí přístup k databázi
    //běžných potravin i restauračních menu a obsahuje pokročilé NLP (přirozené zpracování jazyka) pro analýzu textu
    //(např. text "1 banán a miska ovesných vloček" převede na přesná nutriční data)."
    public class NutritionixService
    {
        private readonly HttpClient _httpClient = new();
        private static readonly string AppId = Secrets.NutritionixAppId;
        private static readonly string ApiKey = Secrets.NutritionixApiKey;

        public async Task<List<ParsedIngredient>?> ParseNaturalTextAsync(string queryText)
        {
            var url = "https://trackapi.nutritionix.com/v2/natural/nutrients";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-app-id", AppId);
            _httpClient.DefaultRequestHeaders.Add("x-app-key", ApiKey);

            var payload = new { query = queryText };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode) return null;

                // 1. Načteme to jen jako hloupý text
                var contentString = await response.Content.ReadAsStringAsync();

                // 2. Zkontrolujeme, jestli to vůbec vypadá jako JSON (JSON vždy začíná { nebo [ )
                if (string.IsNullOrWhiteSpace(contentString) || (!contentString.StartsWith('{') && !contentString.StartsWith('[')))
                {
                    return null; // Zabalíme to dřív, než to stihne spadnout
                }

                // 3. Až teď to bezpečně převedeme
                var root = JsonSerializer.Deserialize<JsonElement>(contentString);
                var parsedList = new List<ParsedIngredient>();

                if (root.TryGetProperty("foods", out var foods))
                {
                    foreach (var food in foods.EnumerateArray())
                    {
                        parsedList.Add(new ParsedIngredient
                        {
                            Name = food.GetProperty("food_name").GetString() ?? "",
                            WeightGrams = food.GetProperty("serving_weight_grams").GetDouble(),
                            Calories = food.GetProperty("nf_calories").GetDouble(),
                            Protein = food.GetProperty("nf_protein").GetDouble(),
                            Carbs = food.GetProperty("nf_total_carbohydrate").GetDouble(),
                            Fat = food.GetProperty("nf_total_fat").GetDouble(),
                            Sugar = food.TryGetProperty("nf_sugars", out var sugarProp) && sugarProp.ValueKind == JsonValueKind.Number
                            ? sugarProp.GetDouble()
                            : 0
                        });
                    }
                }
                return parsedList;
            }
            catch
            {
                return null;
            }
        }
    }

    public class ParsedIngredient
    {
        public string Name { get; set; } = string.Empty;
        public double WeightGrams { get; set; }
        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Sugar { get; set; }
    }
}