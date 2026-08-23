using MobilniKucharka.Classes.Recipe;
using SQLite;
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

                    // Dřív se sem IngredientsRaw vůbec neplnilo - "extendedIngredients" z odpovědi API
                    // se úplně ignorovalo, takže Spoonacular recepty vždy skončily bez surovin (na
                    // rozdíl od MealDB importu, který IngredientsRaw plní správně). Viz ExtractIngredientsRaw níže.
                    IngredientsRaw = ExtractIngredientsRaw(data),

                    // 0 = neznámý počet porcí (viz ServingSize konvence v CLAUDE.md - nikdy nehádat,
                    // vždy se zeptat uživatele). Dřív se sem omylem dosazovala 1 i když "servings"
                    // chybělo, takže recept vypadal, že appka ví, pro kolik lidí je, i když nevěděla.
                    ServingSize = data.TryGetProperty("servings", out var servingsProp) && servingsProp.GetInt32() > 0
                        ? servingsProp.GetInt32()
                        : 0,
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

        // Hledání receptů podle textového dotazu (dotaz už bývá anglicky - viz RecipeSearchService,
        // který ho před voláním přeloží). Vrací jen lehká data (id/název/obrázek) - plné detaily se
        // dotáhnou (a rovnou uloží do DB, viz GetRecipeWithCacheAsync) až po výběru receptu.
        public async Task<List<ExternalRecipeSearchResult>> SearchRecipesAsync(string query, string? diet, CancellationToken cancellationToken)
        {
            string dietParam = string.IsNullOrWhiteSpace(diet) ? "" : $"&diet={Uri.EscapeDataString(diet)}";
            string url = $"https://api.spoonacular.com/recipes/complexSearch?apiKey={ApiKey}&query={Uri.EscapeDataString(query)}&number=10{dietParam}";

            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode) return [];

                var contentString = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(contentString) || !contentString.TrimStart().StartsWith('{'))
                    return [];

                var root = JsonSerializer.Deserialize<JsonElement>(contentString);
                if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                    return [];

                var list = new List<ExternalRecipeSearchResult>();
                foreach (var item in results.EnumerateArray())
                {
                    list.Add(new ExternalRecipeSearchResult
                    {
                        Source = ExternalRecipeSource.Spoonacular,
                        ExternalId = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32().ToString() : "",
                        Name = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "",
                        ImageUrl = item.TryGetProperty("image", out var imgProp) ? imgProp.GetString() ?? "" : ""
                    });
                }
                return list;
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

        // Sestaví IngredientsRaw ve stejném formátu "Název|Množství", jaký používá MealDB import
        // i ruční tvorba receptu (viz CreateRecipePage.TriggerAutoSaveAsync) - tedy jméno a
        // množství oddělené "|", jedna surovina na řádek. Množství bereme z "measures.metric" (ne
        // "measures.us"), aby jednotky (g/ml/kg/l) odpovídaly tomu, co appka jinde umí parsovat
        // (viz NutritionEstimationService.ConvertToProductUnit/DetectUnitFamily).
        private static string ExtractIngredientsRaw(JsonElement root)
        {
            var lines = new List<string>();
            try
            {
                if (!root.TryGetProperty("extendedIngredients", out var ingredients) || ingredients.ValueKind != JsonValueKind.Array)
                    return string.Empty;

                foreach (var ing in ingredients.EnumerateArray())
                {
                    string name = ing.TryGetProperty("nameClean", out var nameCleanProp) && !string.IsNullOrWhiteSpace(nameCleanProp.GetString())
                        ? nameCleanProp.GetString()!
                        : ing.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";

                    if (string.IsNullOrWhiteSpace(name)) continue;

                    double amount = 0;
                    string unit = "";

                    if (ing.TryGetProperty("measures", out var measures) && measures.TryGetProperty("metric", out var metric))
                    {
                        amount = metric.TryGetProperty("amount", out var amtProp) ? amtProp.GetDouble() : 0;
                        unit = metric.TryGetProperty("unitShort", out var unitProp) ? unitProp.GetString() ?? "" : "";
                    }

                    // Fallback na top-level "amount"/"unit", kdyby "measures.metric" chybělo.
                    if (amount <= 0 && ing.TryGetProperty("amount", out var fallbackAmountProp))
                    {
                        amount = fallbackAmountProp.GetDouble();
                        unit = ing.TryGetProperty("unit", out var fallbackUnitProp) ? fallbackUnitProp.GetString() ?? "" : unit;
                    }

                    string normalizedUnit = NormalizeMetricUnit(unit);
                    string amountText = amount > 0 ? $"{amount:0.##} {normalizedUnit}".Trim() : "";

                    lines.Add(string.IsNullOrWhiteSpace(amountText) ? name : $"{name}|{amountText}");
                }
            }
            catch { }
            return string.Join("\n", lines);
        }

        // Sjednotí Spoonacularovy metrické jednotky na tvar, který appka jinde rozpoznává
        // (g/kg/ml/l). Prázdná jednotka (kusové suroviny jako "1 vejce") se bere jako "ks".
        private static string NormalizeMetricUnit(string unit)
        {
            string normalized = unit.Trim().ToLowerInvariant();
            return normalized switch
            {
                "g" or "grams" or "gram" => "g",
                "kg" or "kilograms" or "kilogram" => "kg",
                "ml" or "milliliters" or "milliliter" => "ml",
                "l" or "liters" or "liter" => "l",
                "" => "ks",
                _ => normalized // neznámá jednotka (např. "clove") - necháme tak, ať je surovina aspoň vidět
            };
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