using System.Text.Json;

namespace MobilniKucharka.Services.Api
{
    public class TheMealDbService
    {
        private readonly HttpClient _httpClient = new();
        private readonly NutritionixService _nutritionixService = new();
        private static readonly Random _random = new();

        public async Task<MealDbRecipe?> GetRandomRecipeMatchingDietAsync(List<string> userDiets)
        {
            string? category = null;
            if (userDiets.Contains("Vegan")) category = "Vegan";
            else if (userDiets.Contains("Vegetarian")) category = "Vegetarian";

            if (category != null)
            {
                var ids = await GetMealIdsByCategoryAsync(category);
                if (ids.Count > 0)
                {
                    string randomId = ids[_random.Next(ids.Count)];
                    var byId = await GetRecipeByExternalIdAsync(randomId);
                    if (byId != null) return byId;
                }
            }

            return await GetRandomRecipeAsync();
        }

        public async Task<MealDbRecipe?> GetRandomRecipeAsync()
        {
            return await FetchSingleRecipeAsync("https://www.themealdb.com/api/json/v1/1/random.php");
        }

        public async Task<MealDbRecipe?> GetRecipeByExternalIdAsync(string mealId)
        {
            return await FetchSingleRecipeAsync($"https://www.themealdb.com/api/json/v1/1/lookup.php?i={mealId}");
        }

        // Hledání receptů podle názvu (dotaz už bývá anglicky - viz RecipeSearchService, který ho
        // před voláním přeloží). search.php vrací rovnou kompletní data pro každý nalezený recept,
        // takže tady zatím NEřešíme nutrici - ta se dotáhne až pro konkrétně vybraný recept přes
        // CompleteRecipeWithNutritionAsync, aby se Nutritionix nevolal zbytečně pro celý seznam.
        public async Task<List<MealDbRecipe>> SearchByNameAsync(string name, CancellationToken cancellationToken)
        {
            string url = $"https://www.themealdb.com/api/json/v1/1/search.php?s={Uri.EscapeDataString(name)}";
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode) return [];

                var contentString = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(contentString) || (!contentString.StartsWith('{') && !contentString.StartsWith('[')))
                    return [];

                var root = JsonSerializer.Deserialize<JsonElement>(contentString);
                if (!root.TryGetProperty("meals", out var meals) || meals.ValueKind != JsonValueKind.Array)
                    return [];

                var results = new List<MealDbRecipe>();
                foreach (var meal in meals.EnumerateArray())
                    results.Add(ParseMealElement(meal));

                return results;
            }
            catch (OperationCanceledException)
            {
                throw; // ať volající (RecipeSearchService/SearchPage) pozná rozdíl mezi timeoutem a "nic se nenašlo"
            }
            catch
            {
                return [];
            }
        }

        // Dopočítá nutrici (přes Nutritionix, případně lokální odhad) pro recept už vybraný
        // uživatelem ze seznamu výsledků hledání - viz SearchByNameAsync výše.
        public async Task<MealDbRecipe> CompleteRecipeWithNutritionAsync(MealDbRecipe recipe)
        {
            var ingredientPairs = recipe.Ingredients.Select(i => (i.Name, i.Measure)).ToList();
            await FillNutritionAsync(recipe, ingredientPairs);
            return recipe;
        }

        private async Task<List<string>> GetMealIdsByCategoryAsync(string category)
        {
            string url = $"https://www.themealdb.com/api/json/v1/1/filter.php?c={category}";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return [];

                var contentString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(contentString) || (!contentString.StartsWith('{') && !contentString.StartsWith('[')))
                    return [];

                var root = JsonSerializer.Deserialize<JsonElement>(contentString);
                if (!root.TryGetProperty("meals", out var meals) || meals.ValueKind != JsonValueKind.Array)
                    return [];

                var ids = new List<string>();
                foreach (var meal in meals.EnumerateArray())
                {
                    if (meal.TryGetProperty("idMeal", out var idProp))
                    {
                        string? id = idProp.GetString();
                        if (!string.IsNullOrEmpty(id)) ids.Add(id);
                    }
                }
                return ids;
            }
            catch
            {
                return [];
            }
        }

        private async Task<MealDbRecipe?> FetchSingleRecipeAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var contentString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(contentString) || (!contentString.StartsWith('{') && !contentString.StartsWith('[')))
                    return null;

                var root = JsonSerializer.Deserialize<JsonElement>(contentString);
                if (!root.TryGetProperty("meals", out var meals) || meals.ValueKind != JsonValueKind.Array || meals.GetArrayLength() == 0)
                    return null;

                var recipe = ParseMealElement(meals[0]);
                await FillNutritionAsync(recipe, [.. recipe.Ingredients.Select(i => (i.Name, i.Measure))]);

                return recipe;
            }
            catch
            {
                return null;
            }
        }

        // Sdílený parser jednoho "meal" JSON elementu na MealDbRecipe - používá ho jak
        // FetchSingleRecipeAsync (random/lookup), tak SearchByNameAsync (search.php).
        // Nutrici záměrně neplní - to dělá až volající (FillNutritionAsync/CompleteRecipeWithNutritionAsync).
        private static MealDbRecipe ParseMealElement(JsonElement meal)
        {
            var recipe = new MealDbRecipe
            {
                ExternalId = meal.TryGetProperty("idMeal", out var idProp) ? idProp.GetString() ?? "" : "",
                Name = meal.TryGetProperty("strMeal", out var nameProp) ? nameProp.GetString() ?? "" : "",
                Category = meal.TryGetProperty("strCategory", out var catProp) ? catProp.GetString() ?? "" : "",
                Instructions = meal.TryGetProperty("strInstructions", out var instrProp) ? instrProp.GetString() ?? "" : "",
                ImageUrl = meal.TryGetProperty("strMealThumb", out var thumbProp) ? thumbProp.GetString() ?? "" : "",
                SourceUrl = meal.TryGetProperty("strSource", out var srcProp) ? srcProp.GetString() ?? "" : ""
            };

            var rawIngredients = ExtractIngredients(meal);
            recipe.Ingredients = [.. rawIngredients.Select(i => new MealDbIngredient { Name = i.Ingredient, Measure = i.Measure })];

            return recipe;
        }

        private static List<(string Ingredient, string Measure)> ExtractIngredients(JsonElement meal)
        {
            var list = new List<(string, string)>();

            for (int i = 1; i <= 20; i++)
            {
                if (!meal.TryGetProperty($"strIngredient{i}", out var ingredientProp)) break;

                string ingredient = ingredientProp.GetString() ?? "";
                string measure = meal.TryGetProperty($"strMeasure{i}", out var measureProp)
                    ? (measureProp.GetString() ?? "")
                    : "";

                if (string.IsNullOrWhiteSpace(ingredient)) continue;

                list.Add((ingredient.Trim(), measure.Trim()));
            }

            return list;
        }

        private async Task FillNutritionAsync(MealDbRecipe recipe, List<(string Ingredient, string Measure)> ingredients)
        {
            if (ingredients.Count == 0) return;

            string queryText = string.Join(", ", ingredients.Select(i =>
                string.IsNullOrWhiteSpace(i.Measure) ? i.Ingredient : $"{i.Measure} {i.Ingredient}"));

            var parsed = await _nutritionixService.ParseNaturalTextAsync(queryText);

            if (parsed != null && parsed.Count > 0)
            {
                recipe.Protein = Math.Round(parsed.Sum(p => p.Protein), 1);
                recipe.Carbs = Math.Round(parsed.Sum(p => p.Carbs), 1);
                recipe.Fat = Math.Round(parsed.Sum(p => p.Fat), 1);
                recipe.Sugar = Math.Round(parsed.Sum(p => p.Sugar), 1);
                recipe.Kcal = Math.Round(parsed.Sum(p => p.Calories), 0);
                return; // IsNutritionEstimated zůstává false (výchozí hodnota) - jde o reálná data
            }

            var (Protein, Carbs, Fat, Sugar) = NutritionEstimationService.EstimateNutrition([.. ingredients.Select(i => (i.Ingredient, i.Measure))]);
            recipe.Protein = Protein;
            recipe.Carbs = Carbs;
            recipe.Fat = Fat;
            recipe.Sugar = Sugar;
            recipe.IsNutritionEstimated = true; // <- sem to patří
        }

        // Odhad dietních příznaků z MealDB kategorie - použito jen pro filtrování výsledků hledání
        // (viz RecipeSearchService). Stejná logika žije i jako soukromá kopie v
        // BudgetPlannerService.SaveExternalRecipeAsync - úmyslně duplikováno, ať se nemusí sahat
        // do velkého hlavního souboru kvůli jedné drobné funkci.
        public static List<string> GuessDietFlagsFromCategory(string mealDbCategory)
        {
            return mealDbCategory switch
            {
                "Vegan" => ["Vegan", "Vegetarian"],
                "Vegetarian" => ["Vegetarian"],
                _ => []
            };
        }
    }

    public class MealDbRecipe
    {
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public List<MealDbIngredient> Ingredients { get; set; } = [];

        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Sugar { get; set; }
        public double Kcal { get; set; }
        public bool IsNutritionEstimated { get; set; }
    }

    public class MealDbIngredient
    {
        public string Name { get; set; } = string.Empty;
        public string Measure { get; set; } = string.Empty;
    }
}