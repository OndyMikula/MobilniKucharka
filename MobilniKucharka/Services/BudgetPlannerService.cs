using SQLite;
using MobilniKucharka.Classes;
using System.Text.Json;
using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Classes.UserData.Bookmark;
using MobilniKucharka.Services.Api;
using System.Diagnostics;
using MobilniKucharka.Classes.UserData; // Přidáno pro Debug.WriteLine

namespace MobilniKucharka.Services
{
    public class BudgetPlannerService(string dbPath)
    {
        private readonly SQLiteAsyncConnection _db = new(dbPath);
        private bool _isInitialized;

        private List<LocalProduct>? _cachedProducts;
        private List<RecipeIngredient>? _cachedIngredients;

        private async Task<List<LocalProduct>> GetProductsCachedAsync()
        {
            _cachedProducts ??= await _db.Table<LocalProduct>().ToListAsync();
            return _cachedProducts;
        }

        private async Task<List<RecipeIngredient>> GetIngredientsCachedAsync()
        {
            _cachedIngredients ??= await _db.Table<RecipeIngredient>().ToListAsync();
            return _cachedIngredients;
        }

        // Tuto metodu zavoláme na začátku každé veřejné metody v této službě!
        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Vytvoření tabulek
                await _db.CreateTableAsync<Recipe>();
                await _db.CreateTableAsync<LocalProduct>();
                await _db.CreateTableAsync<LocalProductAlias>();
                await _db.CreateTableAsync<RecipeIngredient>();
                await _db.CreateTableAsync<Bookmark>();
                await _db.CreateTableAsync<RecipeBookmark>();

                var bookmarkCount = await _db.Table<Bookmark>().CountAsync();
                if (bookmarkCount == 0)
                {
                    await SeedBookmarksAsync();
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při inicializaci databáze: {ex.Message}");
                // Tady nechceme házet chybu dál, jen to zaznamenáme.
            }
        }

        public async Task<List<Recipe>> GetAllRecipesAsync()
        {
            await EnsureInitializedAsync();
            return await _db.Table<Recipe>().ToListAsync();
        }

        private async Task SeedBookmarksAsync()
        {
            var defaultBookmarks = new List<Bookmark>
            {
                new() { Name = "Oblíbené", BackgroundColor = "#FFE0E0", Icon = "❤️" },
                new() { Name = "Vytvořené recepty", BackgroundColor = "#E3F2FD", Icon = "👨‍🍳" },
                new() { Name = "Koncepty", BackgroundColor = "#F5F5F5", Icon = "📝" }
            };

            foreach (var b in defaultBookmarks)
                await _db.InsertAsync(b);
        }

        private async Task SeedDatabaseAsync()
        {
            // 1. Základní suroviny s reálnějšími cenami napříč všemi obchody (odhad, Kč)
            var products = new List<LocalProduct>
            {
                new() { Id = 1, Name_CS = "Špagety", Name_EN = "Spaghetti", Unit = "g", PriceAverage = 0.028 },
                new() { Id = 2, Name_CS = "Rajčatová omáčka", Name_EN = "Tomato Sauce", Unit = "ml", PriceAverage = 0.082 },
                new() { Id = 3, Name_CS = "Vejce", Name_EN = "Eggs", Unit = "ks", PriceAverage = 4.5 },
                new() { Id = 4, Name_CS = "Máslo", Name_EN = "Butter", Unit = "g", PriceAverage = 0.142 }, // ČSÚ 6/2026: 141,95 Kč/kg

                // Nové, z ověřených dat ČSÚ (CEN02, spotřebitelské ceny, červen 2026)
                new() { Id = 5, Name_CS = "Hovězí maso zadní bez kosti", Name_EN = "Beef (boneless round)", Unit = "g", PriceAverage = 0.325 }, // 325,41 Kč/kg
                new() { Id = 6, Name_CS = "Vepřová kýta bez kosti", Name_EN = "Pork leg (boneless)", Unit = "g", PriceAverage = 0.104 },        // 104,10 Kč/kg
                new() { Id = 7, Name_CS = "Kuřecí maso celé", Name_EN = "Whole chicken", Unit = "g", PriceAverage = 0.064 },                    // 63,66 Kč/kg
                new() { Id = 8, Name_CS = "Mléko polotučné", Name_EN = "Semi-skimmed milk", Unit = "ml", PriceAverage = 0.022 },                // 22,45 Kč/l
                new() { Id = 9, Name_CS = "Eidam", Name_EN = "Edam cheese", Unit = "g", PriceAverage = 0.177 },                                 // 176,85 Kč/kg
                new() { Id = 10, Name_CS = "Hladká mouka", Name_EN = "Plain flour", Unit = "g", PriceAverage = 0.014 },                         // 14,23 Kč/kg
                new() { Id = 11, Name_CS = "Brambory", Name_EN = "Potatoes", Unit = "g", PriceAverage = 0.020 },                                // 19,68 Kč/kg
                new() { Id = 12, Name_CS = "Jablka", Name_EN = "Apples", Unit = "g", PriceAverage = 0.037 }                                     // 36,65 Kč/kg
            };

            foreach (var prod in products)
            {
                await _db.InsertOrReplaceAsync(prod);
            }

            // 2. Ukázkové recepty — nutriční hodnoty dopočítány z reálného složení
            // (100g těstovin + 150ml omáčky na osobu / 3 vejce + 15g másla na osobu)
            var r1 = new Recipe
            {
                Id = 1,
                Name_CS = "Špagety s rajčatovou omáčkou",
                Name_EN = "Spaghetti with Tomato Sauce",
                PrepTime = 15,
                Protein = 15,
                Carbs = 85,
                Fat = 5,
                Sugar = 11,
                ImageUrl = "https://images.unsplash.com/photo-1546549032-9571cd6b27df?w=500",
                StepsJson_CS = JsonSerializer.Serialize(new List<string> { "Dej vařit vodu na špagety.", "Osol vodu a uvař špagety al dente.", "Ohřej rajčatovou omáčku a promíchej ji s těstovinami." }),
                StepsJson_EN = JsonSerializer.Serialize(new List<string> { "Boil water for spaghetti.", "Salt the water and cook spaghetti al dente.", "Heat the tomato sauce and mix with pasta." }),
                EquipmentJson = JsonSerializer.Serialize(new List<string> { "Hrnec", "Cedník" }),
                DietaryFlagsJson = JsonSerializer.Serialize(new List<string> { "Vegetarian" })
            };

            var r2 = new Recipe
            {
                Id = 2,
                Name_CS = "Míchaná vajíčka na másle",
                Name_EN = "Scrambled Eggs on Butter",
                PrepTime = 5,
                Protein = 19,
                Carbs = 2,
                Fat = 27,
                Sugar = 1,
                ImageUrl = "https://images.unsplash.com/photo-1525351484163-7529414344d8?w=500",
                StepsJson_CS = JsonSerializer.Serialize(new List<string> { "Rozpusť na pánvi máslo.", "Rozklepni vajíčka a míchej na mírném ohni do krémova.", "Osol a ihned podávej." }),
                StepsJson_EN = JsonSerializer.Serialize(new List<string> { "Melt butter in a pan.", "Crack the eggs and stir over low heat until creamy.", "Salt and serve immediately." }),
                EquipmentJson = JsonSerializer.Serialize(new List<string> { "Pánev" }),
                DietaryFlagsJson = JsonSerializer.Serialize(new List<string> { "GlutenFree", "Vegetarian" })
            };

            await _db.InsertOrReplaceAsync(r1);
            await _db.InsertOrReplaceAsync(r2);

            // 3. Propojení surovin s recepty (množství na JEDNU osobu)
            var ingredients = new List<RecipeIngredient>
            {
                new() { RecipeId = 1, ProductId = 1, AmountPerPerson = 100 },
                new() { RecipeId = 1, ProductId = 2, AmountPerPerson = 150 },
                new() { RecipeId = 2, ProductId = 3, AmountPerPerson = 3 },
                new() { RecipeId = 2, ProductId = 4, AmountPerPerson = 15 }
            };

            foreach (var ing in ingredients)
            {
                await _db.InsertOrReplaceAsync(ing);
            }
        }

        public async Task<List<RecipeWithCost>> GetPlanAsync()
        {
            try
            {
                await EnsureInitializedAsync();

                var recipes = (await _db.Table<Recipe>().ToListAsync()).Where(r => !r.IsDraft).ToList();
                var allProducts = await GetProductsCachedAsync();
                var allIngredients = await GetIngredientsCachedAsync();

                var results = new List<RecipeWithCost>();

                double maxDailyBudget = Preferences.Default.Get("WeeklyBudget", 2000.0) / 7.0;
                int peopleCount = Preferences.Default.Get("PeopleCount", 2);

                var userDiets = ParseCommaList(Preferences.Default.Get("UserDiets", ""));
                var userEquipment = ParseCommaList(Preferences.Default.Get("UserAppliances", ""));

                foreach (var recipe in recipes)
                {
                    if (userDiets.Count != 0 && !recipe.DietaryFlags.Any(d => userDiets.Contains(d)))
                        continue;

                    if (userEquipment.Count != 0 && !recipe.Equipment.All(e => userEquipment.Contains(e)))
                        continue;

                    double cost = CalculateRecipeCostInMemory(recipe, peopleCount, allProducts, allIngredients);

                    results.Add(new RecipeWithCost
                    {
                        Recipe = recipe,
                        CalculatedCost = cost,
                        IsWithinBudget = cost <= maxDailyBudget
                    });
                }

                return [.. results.OrderBy(r => r.CalculatedCost)];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při načítání plánu: {ex.Message}");
                return [];
            }
        }

        public async Task<List<Recipe>> GetRecipesByCategoryAsync(string categoryName)
        {
            try
            {
                await EnsureInitializedAsync();

                var links = await _db.Table<RecipeBookmark>()
                                      .Where(rb => rb.CategoryName == categoryName)
                                      .ToListAsync();

                if (links.Count == 0) return [];

                var recipeIds = links.Select(l => l.RecipeId).ToHashSet();
                var allRecipes = await _db.Table<Recipe>().ToListAsync();

                return [.. allRecipes.Where(r => recipeIds.Contains(r.Id))];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při načítání kategorie: {ex.Message}");
                return [];
            }
        }

        public async Task<double> CalculateRecipeCostAsync(int recipeId, int peopleCount)
        {
            try
            {
                await EnsureInitializedAsync();

                var recipeIngredients = await _db.Table<RecipeIngredient>().Where(x => x.RecipeId == recipeId).ToListAsync();
                double totalCost = 0;

                foreach (var ing in recipeIngredients)
                {
                    var product = await _db.Table<LocalProduct>().Where(p => p.Id == ing.ProductId).FirstOrDefaultAsync();
                    if (product != null)
                        totalCost += ing.AmountPerPerson * peopleCount * product.EffectivePrice;
                }

                if (totalCost <= 0)
                {
                    var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
                    if (recipe != null && recipe.ManualCost > 0)
                        return Math.Round(recipe.ManualCost, 0);
                }

                return Math.Round(totalCost, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při výpočtu ceny: {ex.Message}");
                return 0;
            }
        }

        private static double CalculateRecipeCostInMemory(Recipe recipe, int peopleCount, List<LocalProduct> allProducts, List<RecipeIngredient> allIngredients)
        {
            var recipeIngredients = allIngredients.Where(x => x.RecipeId == recipe.Id);
            double totalCost = 0;

            foreach (var ing in recipeIngredients)
            {
                var product = allProducts.FirstOrDefault(p => p.Id == ing.ProductId);
                if (product != null)
                    totalCost += ing.AmountPerPerson * peopleCount * product.EffectivePrice;
            }

            if (totalCost <= 0 && recipe.ManualCost > 0)
                return Math.Round(recipe.ManualCost, 0);

            return Math.Round(totalCost, 0);
        }

        public async Task<Recipe> SaveExternalRecipeAsync(MealDbRecipe mealDbRecipe)
        {
            await EnsureInitializedAsync();

            string externalId = $"mealdb_{mealDbRecipe.ExternalId}";

            var existing = await _db.Table<Recipe>().Where(r => r.ExternalSourceId == externalId).FirstOrDefaultAsync();
            if (existing != null) return existing; // Už jsme tenhle recept jednou stáhli, nevkládáme duplicitu

            var recipe = new Recipe
            {
                Name_CS = mealDbRecipe.Name,
                Name_EN = mealDbRecipe.Name,
                ExternalSourceId = externalId,
                ImageUrl = mealDbRecipe.ImageUrl,
                Category = "Objevené recepty",
                Protein = mealDbRecipe.Protein,
                Carbs = mealDbRecipe.Carbs,
                Fat = mealDbRecipe.Fat,
                Sugar = mealDbRecipe.Sugar,
                StepsJson_CS = JsonSerializer.Serialize(SplitInstructions(mealDbRecipe.Instructions)),
                StepsJson_EN = JsonSerializer.Serialize(SplitInstructions(mealDbRecipe.Instructions)),
                EquipmentJson = "[]", // MealDB neříká, jaké vybavení je potřeba -> nikdy se nevyfiltruje kvůli vybavení
                DietaryFlagsJson = JsonSerializer.Serialize(GuessDietFlags(mealDbRecipe.Category)),
                IngredientsRaw = string.Join("\n", mealDbRecipe.Ingredients.Select(i => $"{i.Name}|{i.Measure}")),
                IsNutritionEstimated = mealDbRecipe.IsNutritionEstimated,
            };

            await _db.InsertAsync(recipe);
            return recipe;
        }

        private static readonly System.Text.RegularExpressions.Regex StandaloneNumberRegex =
    new(@"^\d{1,2}[\.\)]?$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private const string DecorativeMarkerChars = "☐☑☒□■◻◼⬜⬛•▪◦✓✔✗✘-*";

        private static bool IsDecorativeMarkerOnly(string line)
        {
            return line.Length > 0 && line.All(c => DecorativeMarkerChars.Contains(c) || char.IsWhiteSpace(c));
        }

        private static List<string> SplitInstructions(string instructions)
        {
            if (string.IsNullOrWhiteSpace(instructions)) return [];

            var rawLines = instructions
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var cleaned = new List<string>();

            for (int i = 0; i < rawLines.Count; i++)
            {
                string line = rawLines[i];

                // Čistě dekorativní řádek (prázdné zaškrtávátko apod. bez textu) -> zahodit
                if (IsDecorativeMarkerOnly(line))
                    continue;

                // Osamocené číslo kroku -> spojit s následujícím řádkem, např. "3" + "Baking" -> "3 - Baking"
                if (StandaloneNumberRegex.IsMatch(line) && i < rawLines.Count - 1)
                {
                    string number = line.TrimEnd('.', ')');
                    string nextLine = rawLines[i + 1];
                    cleaned.Add($"{number} - {nextLine}");
                    i++; // řádek, který jsme právě spojili, přeskočíme
                    continue;
                }

                string withoutPrefix = System.Text.RegularExpressions.Regex
                    .Replace(line, @"^(\d+[\.\)]\s*|STEP\s*\d+[:\.]?\s*)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    .Trim();

                if (!string.IsNullOrWhiteSpace(withoutPrefix))
                    cleaned.Add(withoutPrefix);
            }

            return cleaned;
        }

        private static List<string> GuessDietFlags(string mealDbCategory)
        {
            return mealDbCategory switch
            {
                "Vegan" => [.. new List<string> { "Vegan", "Vegetarian" }],
                "Vegetarian" => [.. new List<string> { "Vegetarian" }],
                _ => []
            };
        }

        public async Task<List<RecipeWithCost>> SearchRecipesAsync(string searchText, bool applyPreferences)
        {
            try
            {
                await EnsureInitializedAsync();

                var allRecipes = (await _db.Table<Recipe>().ToListAsync()).Where(r => !r.IsDraft).ToList();

                var matches = string.IsNullOrWhiteSpace(searchText)
                    ? allRecipes
                    : [.. allRecipes.Where(r =>
                        r.Name_CS.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                        r.Name_EN.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                      )];

                if (applyPreferences)
                {
                    var userDiets = ParseCommaList(Preferences.Default.Get("UserDiets", ""));
                    var userEquipment = ParseCommaList(Preferences.Default.Get("UserAppliances", ""));

                    matches = [.. matches.Where(r =>
                        (userDiets.Count == 0 || r.DietaryFlags.Any(d => userDiets.Contains(d))) &&
                        (userEquipment.Count == 0 || r.Equipment.All(e => userEquipment.Contains(e)))
                    )];
                }

                int peopleCount = Preferences.Default.Get("PeopleCount", 2);
                double maxDailyBudget = Preferences.Default.Get("WeeklyBudget", 2000.0) / 7.0;

                var allProducts = await GetProductsCachedAsync();
                var allIngredients = await GetIngredientsCachedAsync();

                var results = matches.Select(r =>
                {
                    double cost = CalculateRecipeCostInMemory(r, peopleCount, allProducts, allIngredients);
                    return new RecipeWithCost
                    {
                        Recipe = r,
                        CalculatedCost = cost,
                        IsWithinBudget = cost <= maxDailyBudget
                    };
                }).ToList();

                return [.. results.OrderBy(r => r.Recipe.Name_CS)];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při vyhledávání receptů: {ex.Message}");
                return [];
            }
        }

        public async Task<List<DisplayIngredient>> GetIngredientsForRecipeAsync(int recipeId, int peopleCount)
        {
            try
            {
                await EnsureInitializedAsync();

                var allProducts = await _db.Table<LocalProduct>().ToListAsync();
                var recipeIngredients = await _db.Table<RecipeIngredient>().Where(x => x.RecipeId == recipeId).ToListAsync();

                var displayList = new List<DisplayIngredient>();
                string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");

                foreach (var ing in recipeIngredients)
                {
                    var product = allProducts.FirstOrDefault(p => p.Id == ing.ProductId);
                    if (product == null) continue;

                    double totalAmount = ing.AmountPerPerson * peopleCount;
                    double totalCost = Math.Round(totalAmount * product.EffectivePrice, 0);

                    displayList.Add(new DisplayIngredient
                    {
                        ProductId = product.Id,
                        Name = currentLang == "cs" ? product.Name_CS : product.Name_EN,
                        AmountText = $"{totalAmount:G29} {product.Unit}",
                        CostText = totalCost > 0 ? $"{totalCost:N0} Kč" : "? Kč"
                    });
                }

                return displayList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při načítání surovin: {ex.Message}");
                return [];
            }
        }

        // --- MANIPULACE S PRODUKTY ---
        public async Task SaveProductAsync(LocalProduct product)
        {
            await EnsureInitializedAsync();
            await _db.InsertOrReplaceAsync(product);
            _cachedProducts = null;
        }

        public async Task<LocalProduct?> GetProductByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            return await _db.Table<LocalProduct>().Where(p => p.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Bookmark>> GetAllBookmarksAsync()
        {
            await EnsureInitializedAsync();
            var bookmarks = await _db.Table<Bookmark>().ToListAsync();

            bool anyManualOrder = bookmarks.Any(b => b.HasManualOrder);

            return anyManualOrder
                ? [.. bookmarks.OrderByDescending(b => b.IsPinned).ThenBy(b => b.SortOrder)]
                : [.. bookmarks.OrderByDescending(b => b.IsPinned).ThenByDescending(b => b.LastEditedUtc)];
        }

        public async Task<List<string>> GetDistinctCategoriesAsync()
        {
            await EnsureInitializedAsync();
            var bookmarks = await _db.Table<Bookmark>().ToListAsync();
            return [.. bookmarks.Select(b => b.Name)];
        }

        public async Task<List<string>> GetCategoriesForRecipeAsync(int recipeId)
        {
            await EnsureInitializedAsync();
            var links = await _db.Table<RecipeBookmark>()
                                  .Where(rb => rb.RecipeId == recipeId)
                                  .ToListAsync();
            return [.. links.Select(l => l.CategoryName)];
        }

        public async Task AddRecipeToCategoryAsync(int recipeId, string category)
        {
            await EnsureInitializedAsync();
            var existing = await _db.Table<RecipeBookmark>()
                .Where(rb => rb.RecipeId == recipeId && rb.CategoryName == category)
                .FirstOrDefaultAsync();

            if (existing == null)
                await _db.InsertAsync(new RecipeBookmark { RecipeId = recipeId, CategoryName = category });
        }

        public async Task RemoveRecipeFromCategoryAsync(int recipeId, string category)
        {
            await EnsureInitializedAsync();
            var existing = await _db.Table<RecipeBookmark>()
                .Where(rb => rb.RecipeId == recipeId && rb.CategoryName == category)
                .FirstOrDefaultAsync();

            if (existing != null)
                await _db.DeleteAsync(existing);
        }

        public async Task InsertNewCategoryAsync(string category, string imagePath)
        {
            await EnsureInitializedAsync();

            var existing = await _db.Table<Bookmark>().Where(b => b.Name == category).FirstOrDefaultAsync();
            if (existing != null) return;

            var bookmark = new Bookmark { Name = category };

            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                bookmark.BackgroundImage = imagePath;
            else
                bookmark.BackgroundColor = "#2196F3";

            await _db.InsertAsync(bookmark);
        }

        public async Task DeleteRecipeAsync(int recipeId)
        {
            await EnsureInitializedAsync();

            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe != null)
                await _db.DeleteAsync(recipe);

            var links = await _db.Table<RecipeBookmark>().Where(rb => rb.RecipeId == recipeId).ToListAsync();
            foreach (var link in links)
                await _db.DeleteAsync(link);
        }

        public async Task DeleteBookmarkAsync(string categoryName)
        {
            await EnsureInitializedAsync();

            var bookmark = await _db.Table<Bookmark>().Where(b => b.Name == categoryName).FirstOrDefaultAsync();
            if (bookmark != null)
                await _db.DeleteAsync(bookmark);

            var links = await _db.Table<RecipeBookmark>().Where(rb => rb.CategoryName == categoryName).ToListAsync();
            foreach (var link in links)
                await _db.DeleteAsync(link);
        }

        public async Task<Recipe?> GetRecipeByIdAsync(int recipeId)
        {
            await EnsureInitializedAsync();
            return await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
        }

        private static List<string> ParseCommaList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return [];
            return [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }

        public async Task ResetDatabaseAsync()
        {
            await _db.DeleteAllAsync<Recipe>();
            await _db.DeleteAllAsync<LocalProduct>();
            await _db.DeleteAllAsync<RecipeIngredient>();
            await _db.DeleteAllAsync<Bookmark>();
            await _db.DeleteAllAsync<RecipeBookmark>();

            _cachedProducts = null;
            _cachedIngredients = null;
            _isInitialized = false;
            await EnsureInitializedAsync();
        }

        public async Task UpdateRecipeRatingAsync(int recipeId, double newRating)
        {
            await EnsureInitializedAsync();
            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe != null)
            {
                recipe.Rating = newRating;
                await _db.UpdateAsync(recipe);
            }
        }

        public async Task SetManualPriceAsync(int productId, double price)
        {
            await EnsureInitializedAsync();
            var product = await _db.Table<LocalProduct>().Where(p => p.Id == productId).FirstOrDefaultAsync();
            if (product != null)
            {
                product.HasManualPrice = true;
                product.ManualPrice = price;
                await _db.UpdateAsync(product);
                _cachedProducts = null;
            }
        }

        public async Task ClearManualPriceAsync(int productId)
        {
            await EnsureInitializedAsync();
            var product = await _db.Table<LocalProduct>().Where(p => p.Id == productId).FirstOrDefaultAsync();
            if (product != null)
            {
                product.HasManualPrice = false;
                await _db.UpdateAsync(product);
                _cachedProducts = null;
            }
        }

        public async Task TogglePinAsync(string categoryName)
        {
            await EnsureInitializedAsync();
            var bookmark = await _db.Table<Bookmark>().Where(b => b.Name == categoryName).FirstOrDefaultAsync();
            if (bookmark != null)
            {
                bookmark.IsPinned = !bookmark.IsPinned;
                await _db.UpdateAsync(bookmark);
            }
        }

        public async Task UpdateBookmarkOrderAsync(List<string> orderedCategoryNames)
        {
            await EnsureInitializedAsync();
            for (int i = 0; i < orderedCategoryNames.Count; i++)
            {
                string name = orderedCategoryNames[i]; // vyhodnotíme index MIMO LINQ výraz
                var bookmark = await _db.Table<Bookmark>().Where(b => b.Name == name).FirstOrDefaultAsync();
                if (bookmark != null)
                {
                    bookmark.SortOrder = i;
                    bookmark.HasManualOrder = true;
                    await _db.UpdateAsync(bookmark);
                }
            }
        }

        private async Task TouchBookmarkEditedAsync(string categoryName)
        {
            var bookmark = await _db.Table<Bookmark>().Where(b => b.Name == categoryName).FirstOrDefaultAsync();
            if (bookmark != null)
            {
                bookmark.LastEditedUtc = DateTime.UtcNow;
                await _db.UpdateAsync(bookmark);
            }
        }

        public async Task<int> ApplyCsuPricesAsync(List<CsuPriceEntry> csuPrices)
        {
            await EnsureInitializedAsync();
            var products = await _db.Table<LocalProduct>().ToListAsync();
            int updated = 0;

            foreach (var product in products)
            {
                if (product.HasManualPrice) continue; // ruční cena má přednost, nepřepisujeme ji

                var match = csuPrices.FirstOrDefault(p => p.ProductName.Equals(product.Name_CS, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    // ČSÚ ceny jsou za 1 kg/l, naše jednotky jsou g/ml -> přepočet na cenu za 1 g/ml
                    product.PriceAverage = match.Unit is "kg" or "l" ? match.Price / 1000.0 : match.Price;
                    await _db.UpdateAsync(product);
                    updated++;
                }
            }

            _cachedProducts = null;
            return updated;
        }

        public async Task<LocalProduct> GetOrCreateLocalProductByNameAsync(string name)
        {
            await EnsureInitializedAsync();
            string trimmed = name.Trim();

            var allAliases = await _db.Table<LocalProductAlias>().ToListAsync();
            var alias = allAliases.FirstOrDefault(a => string.Equals(a.Alias, trimmed, StringComparison.OrdinalIgnoreCase));

            if (alias != null)
            {
                var aliasedProduct = await _db.Table<LocalProduct>().Where(p => p.Id == alias.ProductId).FirstOrDefaultAsync();
                if (aliasedProduct != null) return aliasedProduct;
            }

            var allProducts = await _db.Table<LocalProduct>().ToListAsync();
            var existing = allProducts.FirstOrDefault(p =>
                string.Equals(p.Name_CS, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name_EN, trimmed, StringComparison.OrdinalIgnoreCase));

            if (existing != null) return existing;

            var newProduct = new LocalProduct
            {
                Name_CS = trimmed,
                Name_EN = trimmed,
                Unit = "g",
                PriceAverage = 0
            };

            await _db.InsertAsync(newProduct);
            _cachedProducts = null;
            return newProduct;
        }

        public async Task LinkIngredientNameToProductAsync(string ingredientName, int existingProductId)
        {
            await EnsureInitializedAsync();
            string trimmed = ingredientName.Trim();

            var allAliases = await _db.Table<LocalProductAlias>().ToListAsync();
            var existingAlias = allAliases.FirstOrDefault(a => string.Equals(a.Alias, trimmed, StringComparison.OrdinalIgnoreCase));

            if (existingAlias != null)
            {
                existingAlias.ProductId = existingProductId;
                await _db.UpdateAsync(existingAlias);
            }
            else
            {
                await _db.InsertAsync(new LocalProductAlias { Alias = trimmed, ProductId = existingProductId });
            }

            _cachedProducts = null;
        }

        public async Task<List<LocalProduct>> GetAllLocalProductsAsync()
        {
            await EnsureInitializedAsync();
            var products = await _db.Table<LocalProduct>().ToListAsync();
            return [.. products.OrderBy(p => p.Name_CS)];
        }
    }

    public class RecipeWithCost
    {
        public Recipe Recipe { get; set; } = null!;
        public double CalculatedCost { get; set; }
        public string CostDisplayText => CalculatedCost > 0 ? $"Cena nákupu: {CalculatedCost:N0} Kč" : "Cena nákupu: ? Kč";
        public bool IsWithinBudget { get; set; }
        public string CostColor => IsWithinBudget ? "#4CAF50" : "#F44336";
        public string BudgetStatusText => IsWithinBudget ? "Vejde se do rozpočtu!" : "Nad denní limit";
    }

    public class DisplayIngredient
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AmountText { get; set; } = string.Empty;
        public string CostText { get; set; } = string.Empty;
    }
}