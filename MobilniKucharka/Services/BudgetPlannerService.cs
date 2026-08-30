using MobilniKucharka.Classes;
using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Classes.UserData.Bookmark;
using MobilniKucharka.Services.Api;
using SQLite;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MobilniKucharka.Services
{
    public partial class BudgetPlannerService(string dbPath)
    {
        private readonly SQLiteAsyncConnection _db = new(dbPath);
        private bool _isInitialized;

        private List<LocalProduct>? _cachedProducts;
        private List<RecipeIngredient>? _cachedIngredients;
        private List<LocalProductAlias>? _cachedAliases;

        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            try
            {
                await _db.CreateTableAsync<Recipe>();
                await _db.CreateTableAsync<LocalProduct>();
                await _db.CreateTableAsync<RecipeIngredient>();
                await _db.CreateTableAsync<Bookmark>();
                await _db.CreateTableAsync<RecipeBookmark>();
                await _db.CreateTableAsync<LocalProductAlias>();

                var recipeCount = await _db.Table<Recipe>().CountAsync();
                if (recipeCount == 0)
                {
                    await SeedDatabaseAsync();
                }

                var bookmarkCount = await _db.Table<Bookmark>().CountAsync();
                if (bookmarkCount == 0)
                {
                    await SeedBookmarksAsync();
                }
                else
                {
                    // Doplňková migrace pro appky, které už měly záložky nasazené z dřívějška -
                    // seed výše se spustí jen na úplně prázdné tabulce, takže existující instalace
                    // (včetně vývojového zařízení) by jinak "Vyhledané recepty" nikdy nedostaly,
                    // aniž by se jim smazala data.
                    await EnsureSearchedRecipesBookmarkExistsAsync();
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při inicializaci databáze: {ex.Message}");
            }
        }

        private async Task SeedDatabaseAsync()
        {
            var products = new List<LocalProduct>
            {
                new() { Id = 1, Name_CS = "Špagety", Name_EN = "Spaghetti", Unit = "g", PriceAverage = 0.028 },
                new() { Id = 2, Name_CS = "Rajčatová omáčka", Name_EN = "Tomato Sauce", Unit = "ml", PriceAverage = 0.082 },
                new() { Id = 3, Name_CS = "Vejce", Name_EN = "Eggs", Unit = "ks", PriceAverage = 4.5 },
                new() { Id = 4, Name_CS = "Máslo", Name_EN = "Butter", Unit = "g", PriceAverage = 0.142 },
                new() { Id = 5, Name_CS = "Hovězí maso zadní bez kosti", Name_EN = "Beef (boneless round)", Unit = "g", PriceAverage = 0.325 },
                new() { Id = 6, Name_CS = "Vepřová kýta bez kosti", Name_EN = "Pork leg (boneless)", Unit = "g", PriceAverage = 0.104 },
                new() { Id = 7, Name_CS = "Kuřecí maso celé", Name_EN = "Whole chicken", Unit = "g", PriceAverage = 0.064 },
                new() { Id = 8, Name_CS = "Mléko polotučné", Name_EN = "Semi-skimmed milk", Unit = "ml", PriceAverage = 0.022 },
                new() { Id = 9, Name_CS = "Eidam", Name_EN = "Edam cheese", Unit = "g", PriceAverage = 0.177 },
                new() { Id = 10, Name_CS = "Hladká mouka", Name_EN = "Plain flour", Unit = "g", PriceAverage = 0.014 },
                new() { Id = 11, Name_CS = "Brambory", Name_EN = "Potatoes", Unit = "g", PriceAverage = 0.020 },
                new() { Id = 12, Name_CS = "Jablka", Name_EN = "Apples", Unit = "g", PriceAverage = 0.037 }
            };

            foreach (var prod in products)
            {
                await _db.InsertOrReplaceAsync(prod);
            }

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
                ServingSize = 1,
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
                ServingSize = 1,
                ImageUrl = "https://images.unsplash.com/photo-1525351484163-7529414344d8?w=500",
                StepsJson_CS = JsonSerializer.Serialize(new List<string> { "Rozpusť na pánvi máslo.", "Rozklepni vajíčka a míchej na mírném ohni do krémova.", "Osol a ihned podávej." }),
                StepsJson_EN = JsonSerializer.Serialize(new List<string> { "Melt butter in a pan.", "Crack the eggs and stir over low heat until creamy.", "Salt and serve immediately." }),
                EquipmentJson = JsonSerializer.Serialize(new List<string> { "Pánev" }),
                DietaryFlagsJson = JsonSerializer.Serialize(new List<string> { "GlutenFree", "Vegetarian" })
            };

            await _db.InsertOrReplaceAsync(r1);
            await _db.InsertOrReplaceAsync(r2);

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

        private async Task SeedBookmarksAsync()
        {
            var defaultBookmarks = new List<Bookmark>
            {
                new() { Name = "Oblíbené", BackgroundColor = "#FFE0E0", Icon = "❤️" },
                new() { Name = "Vytvořené recepty", BackgroundColor = "#E3F2FD", Icon = "👨‍🍳" },
                new() { Name = "Vyhledané recepty", BackgroundColor = "#E8F5E9", Icon = "🔍" },
                new() { Name = "Koncepty", BackgroundColor = "#F5F5F5", Icon = "📝" }
            };

            foreach (var b in defaultBookmarks)
                await _db.InsertAsync(b);
        }

        // Doplní "Vyhledané recepty" u appek, které tuhle záložku ještě nemají (viz komentář u
        // volání v EnsureInitializedAsync výše). Bezpečné volat opakovaně - jakmile záložka jednou
        // existuje, další volání jen zkontroluje a nic nedělá.
        private async Task EnsureSearchedRecipesBookmarkExistsAsync()
        {
            var existing = await _db.Table<Bookmark>().Where(b => b.Name == "Vyhledané recepty").FirstOrDefaultAsync();
            if (existing == null)
            {
                await _db.InsertAsync(new Bookmark { Name = "Vyhledané recepty", BackgroundColor = "#E8F5E9", Icon = "🔍" });
            }
        }

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

        private async Task<List<LocalProductAlias>> GetAliasesCachedAsync()
        {
            _cachedAliases ??= await _db.Table<LocalProductAlias>().ToListAsync();
            return _cachedAliases;
        }

        private static List<string> ParseCommaList(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return [];
            return [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }

        public async Task<List<RecipeWithCost>> GetPlanAsync()
        {
            try
            {
                await EnsureInitializedAsync();

                var recipes = (await _db.Table<Recipe>().ToListAsync()).Where(r => !r.IsDraft && !r.IsSearchTemp).ToList();
                var allProducts = await GetProductsCachedAsync();
                var allIngredients = await GetIngredientsCachedAsync();
                var allAliases = await GetAliasesCachedAsync();

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

                    // Doplní jméno (a kroky) do aktuálního jazyka appky, pokud ještě chybí - díky cache uvnitř
                    // EnsureRecipeLanguageAsync se DeepL zavolá jen jednou za (recept, jazyk) navždy; další
                    // zobrazení seznamu je pak jen levná kontrola v DB, ne nové volání API.
                    var displayRecipe = await EnsureRecipeLanguageAsync(recipe.Id) ?? recipe;

                    var (cost, allPriced, anyPriced) = CalculateFullRecipeCost(displayRecipe, peopleCount, allProducts, allIngredients, allAliases);

                    results.Add(new RecipeWithCost
                    {
                        Recipe = displayRecipe,
                        CalculatedCost = cost,
                        AllIngredientsPriced = allPriced,
                        AnyIngredientsPriced = anyPriced,
                        IsWithinBudget = allPriced && cost <= maxDailyBudget
                    });
                }

                return [.. results
                    .OrderBy(r => r.AllIngredientsPriced ? 0 : (r.AnyIngredientsPriced ? 1 : 2))
                    .ThenBy(r => r.CalculatedCost)];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při načítání plánu: {ex.Message}");
                return [];
            }
        }

        public async Task<List<RecipeWithCost>> SearchRecipesAsync(string searchText, bool applyPreferences)
        {
            try
            {
                await EnsureInitializedAsync();

                var allRecipes = (await _db.Table<Recipe>().ToListAsync()).Where(r => !r.IsDraft && !r.IsSearchTemp).ToList();

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
                var allAliases = await GetAliasesCachedAsync();

                var results = new List<RecipeWithCost>();
                foreach (var match in matches)
                {
                    // Stejná logika jako v GetPlanAsync - doplní překlad jména/kroků, pokud ještě chybí
                    // (např. čerstvě naimportovaný recept ze SearchPage), s cache proti opakovaným DeepL voláním.
                    var displayRecipe = await EnsureRecipeLanguageAsync(match.Id) ?? match;

                    var (cost, allPriced, anyPriced) = CalculateFullRecipeCost(displayRecipe, peopleCount, allProducts, allIngredients, allAliases);

                    results.Add(new RecipeWithCost
                    {
                        Recipe = displayRecipe,
                        CalculatedCost = cost,
                        AllIngredientsPriced = allPriced,
                        AnyIngredientsPriced = anyPriced,
                        IsWithinBudget = allPriced && cost <= maxDailyBudget
                    });
                }

                return [.. results.OrderBy(r => r.Recipe.Name_CS)];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při vyhledávání receptů: {ex.Message}");
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
                var matchedRecipes = allRecipes.Where(r => recipeIds.Contains(r.Id)).ToList();

                // Stejný "translate-on-read" vzor jako GetPlanAsync/SearchRecipesAsync - recept
                // naimportovaný jen v jednom jazyce (např. přes SearchPage, který ukládá jen Name_EN)
                // se tu doplní do aktuálního jazyka appky, pokud ještě nebyl zobrazen přes
                // RecipeDetailPage. Díky tomu se i "Vytvořené recepty" (kam Import odkládá recepty)
                // zobrazují správně přeložené. Cache uvnitř EnsureRecipeLanguageAsync zajistí, že se
                // DeepL nezavolá znovu, pokud už překlad existuje.
                var displayRecipes = new List<Recipe>();
                foreach (var recipe in matchedRecipes)
                {
                    displayRecipes.Add(await EnsureRecipeLanguageAsync(recipe.Id) ?? recipe);
                }

                return displayRecipes;
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

                var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
                if (recipe == null) return 0;

                var allProducts = await GetProductsCachedAsync();
                var allIngredients = await GetIngredientsCachedAsync();
                var allAliases = await GetAliasesCachedAsync();

                var (cost, _, _) = CalculateFullRecipeCost(recipe, peopleCount, allProducts, allIngredients, allAliases);
                return cost;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při výpočtu ceny: {ex.Message}");
                return 0;
            }
        }

        private static (double TotalCost, bool AllPriced, bool AnyPriced) CalculateFullRecipeCost(Recipe recipe, int peopleCount, List<LocalProduct> allProducts, List<RecipeIngredient> allIngredients, List<LocalProductAlias> allAliases)
        {
            var recipeIngredients = allIngredients.Where(x => x.RecipeId == recipe.Id).ToList();

            if (recipeIngredients.Count > 0)
            {
                double total = 0;
                bool allPriced = true;
                bool anyPriced = false;

                foreach (var ing in recipeIngredients)
                {
                    var product = allProducts.FirstOrDefault(p => p.Id == ing.ProductId);
                    if (product == null) { allPriced = false; continue; }

                    double cost = ing.AmountPerPerson * peopleCount * product.EffectivePrice;
                    total += cost;
                    if (cost > 0) anyPriced = true; else allPriced = false;
                }

                if (total > 0) return (Math.Round(total, 0), allPriced, true);
                if (recipe.ManualCost > 0) return (Math.Round(recipe.ManualCost, 0), true, true);
                return (0, false, anyPriced);
            }

            if (!string.IsNullOrWhiteSpace(recipe.IngredientsRaw))
            {
                var lines = recipe.IngredientsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                double total = 0;
                int pricableCount = 0;
                int pricedCount = 0;

                int effectiveServingSize = recipe.ServingSize > 0 ? recipe.ServingSize : 0;
                double scaleFactor = effectiveServingSize > 0 ? peopleCount / (double)effectiveServingSize : 1.0;

                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    string name = parts.ElementAtOrDefault(0)?.Trim() ?? "";
                    string amount = parts.ElementAtOrDefault(1)?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var product = FindProductByNameReadOnly(name, allProducts, allAliases);

                    if (product == null)
                    {
                        if (NutritionEstimationService.TryParseLeadingQuantity(amount, 60) != null)
                            pricableCount++;
                        continue;
                    }

                    double pieceWeight = product.TypicalUnitWeightGrams > 0 ? product.TypicalUnitWeightGrams : 60;
                    double? parsedAmount = NutritionEstimationService.ConvertToProductUnit(amount, product.Unit, pieceWeight);
                    if (parsedAmount == null) continue;

                    pricableCount++;
                    double scaledAmount = parsedAmount.Value * scaleFactor;
                    double cost = Math.Round(scaledAmount * product.EffectivePrice, 0);
                    total += cost;
                    if (cost > 0) pricedCount++;
                }

                bool allPriced = pricableCount > 0 && pricableCount == pricedCount;
                bool anyPriced = pricedCount > 0;

                if (total > 0) return (Math.Round(total, 0), allPriced, anyPriced);
                if (recipe.ManualCost > 0) return (Math.Round(recipe.ManualCost, 0), true, true);
                return (0, false, anyPriced);
            }

            if (recipe.ManualCost > 0) return (Math.Round(recipe.ManualCost, 0), true, true);
            return (0, false, false);
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
                        RawAmount = totalAmount,
                        CostValue = totalCost,
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

        public async Task<List<LocalProduct>> GetAllLocalProductsAsync()
        {
            await EnsureInitializedAsync();
            var products = await _db.Table<LocalProduct>().ToListAsync();
            return [.. products.OrderBy(p => p.Name_CS)];
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

        public async Task SetTypicalUnitWeightAsync(int productId, double gramsPerPiece)
        {
            await EnsureInitializedAsync();
            var product = await _db.Table<LocalProduct>().Where(p => p.Id == productId).FirstOrDefaultAsync();
            if (product != null)
            {
                product.TypicalUnitWeightGrams = gramsPerPiece;
                await _db.UpdateAsync(product);
                _cachedProducts = null;
            }
        }

        public async Task<LocalProduct> GetOrCreateLocalProductByNameAsync(string name, string suggestedUnit = "g")
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
                Unit = suggestedUnit,
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

            await TouchBookmarkEditedAsync(category);
        }

        public async Task RemoveRecipeFromCategoryAsync(int recipeId, string category)
        {
            await EnsureInitializedAsync();
            var existing = await _db.Table<RecipeBookmark>()
                .Where(rb => rb.RecipeId == recipeId && rb.CategoryName == category)
                .FirstOrDefaultAsync();

            if (existing != null)
                await _db.DeleteAsync(existing);

            await TouchBookmarkEditedAsync(category);
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

        // Výchozí čtyři záložky - jejich Name je použitý jako doslovný srovnávací klíč napříč kódem
        // (AddRecipeToCategoryAsync, GetRecipesByCategoryAsync, BookmarksPage.razor.TranslateCategoryName...),
        // takže se nikdy nesmí přejmenovat. Obrázek/popis u nich ale klidně editovatelné jsou.
        private static readonly string[] ProtectedBookmarkNames = ["Oblíbené", "Vytvořené recepty", "Vyhledané recepty", "Koncepty"];

        public async Task InsertNewCategoryAsync(string category, string imagePath, string description = "")
        {
            await EnsureInitializedAsync();

            var existing = await _db.Table<Bookmark>().Where(b => b.Name == category).FirstOrDefaultAsync();
            if (existing != null) return;

            var bookmark = new Bookmark { Name = category, Description = description?.Trim() ?? string.Empty };

            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                bookmark.BackgroundImage = imagePath;
            else
                bookmark.BackgroundColor = "#2196F3";

            await _db.InsertAsync(bookmark);
        }

        public async Task<Bookmark?> GetBookmarkByNameAsync(string categoryName)
        {
            await EnsureInitializedAsync();
            return await _db.Table<Bookmark>().Where(b => b.Name == categoryName).FirstOrDefaultAsync();
        }

        // Upraví existující záložku - obrázek, popis, a (jen u nechráněných záložek) i název.
        // removeImage: true vrátí záložku na výchozí jednobarevné pozadí - hlavně pro obnovu záložek
        // zasažených starým bugem, kdy uživatelem vybraný obrázek zmizel po aktualizaci appky (viz
        // CreateBookmarkPage.OnPickImageClicked, který teď kopíruje soubor do AppDataDirectory natrvalo).
        // Přejmenování u výchozích čtyř záložek je zakázané (viz ProtectedBookmarkNames); pokud se název
        // u nechráněné záložky změní, přepíšou se i všechny navázané RecipeBookmark záznamy, aby recepty
        // v ní zůstaly zachované pod novým názvem. Kolize s existujícím názvem přejmenování potichu zruší,
        // ať se dvě různé záložky nesloučí pod jeden název.
        public async Task UpdateBookmarkAsync(string originalName, string newName, string? imagePath, bool removeImage, string description)
        {
            await EnsureInitializedAsync();

            var bookmark = await _db.Table<Bookmark>().Where(b => b.Name == originalName).FirstOrDefaultAsync();
            if (bookmark == null) return;

            bool isProtected = ProtectedBookmarkNames.Contains(originalName);
            string trimmedNewName = newName.Trim();

            if (!isProtected && !string.IsNullOrWhiteSpace(trimmedNewName) && trimmedNewName != originalName)
            {
                var nameCollision = await _db.Table<Bookmark>().Where(b => b.Name == trimmedNewName).FirstOrDefaultAsync();
                if (nameCollision == null)
                {
                    bookmark.Name = trimmedNewName;

                    var links = await _db.Table<RecipeBookmark>().Where(rb => rb.CategoryName == originalName).ToListAsync();
                    foreach (var link in links)
                    {
                        link.CategoryName = trimmedNewName;
                        await _db.UpdateAsync(link);
                    }
                }
            }

            if (removeImage)
            {
                bookmark.BackgroundImage = string.Empty;
                bookmark.BackgroundColor = "#2196F3";
            }
            else if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                bookmark.BackgroundImage = imagePath;
            }

            bookmark.Description = description?.Trim() ?? string.Empty;
            bookmark.LastEditedUtc = DateTime.UtcNow;

            await _db.UpdateAsync(bookmark);
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

        public async Task<List<Bookmark>> GetAllBookmarksAsync()
        {
            await EnsureInitializedAsync();
            var bookmarks = await _db.Table<Bookmark>().ToListAsync();

            bool anyManualOrder = bookmarks.Any(b => b.HasManualOrder);

            return anyManualOrder
                ? [.. bookmarks.OrderByDescending(b => b.IsPinned).ThenBy(b => b.SortOrder)]
                : [.. bookmarks.OrderByDescending(b => b.IsPinned).ThenBy(b => b.Id)];
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
                string name = orderedCategoryNames[i];
                var bookmark = await _db.Table<Bookmark>().Where(b => b.Name == name).FirstOrDefaultAsync();
                if (bookmark != null)
                {
                    bookmark.SortOrder = i;
                    bookmark.HasManualOrder = true;
                    await _db.UpdateAsync(bookmark);
                }
            }
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

        public async Task<Recipe?> GetRecipeByIdAsync(int recipeId)
        {
            await EnsureInitializedAsync();
            return await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
        }

        public async Task<List<Recipe>> GetAllRecipesAsync()
        {
            await EnsureInitializedAsync();
            var recipes = await _db.Table<Recipe>().ToListAsync();
            return [.. recipes.Where(r => !r.IsSearchTemp)];
        }

        public async Task<Recipe> SaveExternalRecipeAsync(MealDbRecipe mealDbRecipe, string? translatedNameCs = null)
        {
            await EnsureInitializedAsync();
            string externalId = $"mealdb_{mealDbRecipe.ExternalId}";
            var existing = await _db.Table<Recipe>().Where(r => r.ExternalSourceId == externalId).FirstOrDefaultAsync();
            if (existing != null) return existing;
            var recipe = new Recipe
            {
                Name_EN = mealDbRecipe.Name,
                // Pokud appka recept už jednou přeložila pro zobrazení v seznamu výsledků hledání
                // (viz RecipeSearchService.TranslateResultNamesForDisplayAsync), použijeme ten
                // překlad rovnou - ať se za tutéž větu neplatí DeepL kvóta podruhé jen proto, že
                // recept mezitím "přešel" ze seznamu do uloženého receptu. Bez něj zůstane prázdné
                // a doplní ho EnsureRecipeLanguageAsync při prvním zobrazení, stejně jako dřív.
                Name_CS = translatedNameCs ?? string.Empty,
                ExternalSourceId = externalId,
                ImageUrl = mealDbRecipe.ImageUrl,
                Category = "Objevené recepty",
                Protein = mealDbRecipe.Protein,
                Carbs = mealDbRecipe.Carbs,
                Fat = mealDbRecipe.Fat,
                Sugar = mealDbRecipe.Sugar,
                IsNutritionEstimated = mealDbRecipe.IsNutritionEstimated,
                StepsJson_EN = JsonSerializer.Serialize(SplitInstructions(mealDbRecipe.Instructions)),
                // StepsJson_CS necháváme prázdné ze stejného důvodu jako dřív - hledání nikdy
                // nepřekládá kroky, jen zobrazované názvy v seznamu.
                EquipmentJson = "[]",
                DietaryFlagsJson = JsonSerializer.Serialize(GuessDietFlags(mealDbRecipe.Category)),
                IngredientsRaw = string.Join("\n", mealDbRecipe.Ingredients.Select(i => $"{i.Name}|{i.Measure}")),
                SourceUrl = mealDbRecipe.SourceUrl,
                ServingSize = 0
            };
            await _db.InsertAsync(recipe);
            return recipe;
        }

        [GeneratedRegex(@"^\d{1,2}[\.\)]?$")]
        private static partial Regex StandaloneNumberRegexGen();

        [GeneratedRegex(@"^(\d+[\.\)]\s*|STEP\s*\d+[:\.]?\s*)", RegexOptions.IgnoreCase)]
        private static partial Regex StepPrefixRegexGen();

        private const string DecorativeMarkerChars = "☐☑☒□■◻◼⬜⬛•▪◦✓✔✗✘-*";

        [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z])")]
        private static partial Regex SentenceBoundaryRegexGen();

        private static List<string> SplitLongStepIntoSentences(string step)
        {
            var sentences = SentenceBoundaryRegexGen().Split(step);
            return [.. sentences.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s))];
        }

        private static bool IsDecorativeMarkerOnly(string line)
        {
            return line.Length > 0 && line.Length <= 4 && !line.Any(char.IsLetter);
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

                if (IsDecorativeMarkerOnly(line))
                    continue;

                if (StandaloneNumberRegexGen().IsMatch(line) && i < rawLines.Count - 1)
                {
                    string number = line.TrimEnd('.', ')');
                    string nextLine = rawLines[i + 1];
                    cleaned.Add($"{number} - {nextLine}");
                    i++;
                    continue;
                }

                string withoutPrefix = StepPrefixRegexGen().Replace(line, "").Trim();

                if (!string.IsNullOrWhiteSpace(withoutPrefix))
                    cleaned.Add(withoutPrefix);
            }

            if (cleaned.Count <= 2)
            {
                var expanded = new List<string>();
                foreach (var step in cleaned)
                {
                    var sentences = SplitLongStepIntoSentences(step);
                    if (sentences.Count > 1)
                        expanded.AddRange(sentences);
                    else
                        expanded.Add(step);
                }
                if (expanded.Count > cleaned.Count)
                    cleaned = expanded;
            }

            return cleaned;
        }

        private static List<string> GuessDietFlags(string mealDbCategory)
        {
            return mealDbCategory switch
            {
                "Vegan" => ["Vegan", "Vegetarian"],
                "Vegetarian" => ["Vegetarian"],
                _ => []
            };
        }

        public async Task<int> RepairAllRecipeStepsAsync()
        {
            await EnsureInitializedAsync();
            var recipes = await _db.Table<Recipe>().ToListAsync();
            int fixedCount = 0;

            foreach (var recipe in recipes)
            {
                var repairedCs = SplitInstructions(string.Join("\n", recipe.Steps_CS));
                var repairedEn = SplitInstructions(string.Join("\n", recipe.Steps_EN));

                bool changed = !repairedCs.SequenceEqual(recipe.Steps_CS) || !repairedEn.SequenceEqual(recipe.Steps_EN);

                if (changed)
                {
                    recipe.Steps_CS = repairedCs;
                    recipe.Steps_EN = repairedEn;
                    await _db.UpdateAsync(recipe);
                    fixedCount++;
                }
            }

            return fixedCount;
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

        private static LocalProduct? FindProductByNameReadOnly(string name, List<LocalProduct> allProducts, List<LocalProductAlias> allAliases)
        {
            string trimmed = name.Trim();

            var alias = allAliases.FirstOrDefault(a => string.Equals(a.Alias, trimmed, StringComparison.OrdinalIgnoreCase));
            if (alias != null)
            {
                var aliasedProduct = allProducts.FirstOrDefault(p => p.Id == alias.ProductId);
                if (aliasedProduct != null) return aliasedProduct;
            }

            return allProducts.FirstOrDefault(p =>
                string.Equals(p.Name_CS, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name_EN, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<int> ImportSharedRecipeAsync(Recipe recipe)
        {
            await EnsureInitializedAsync();
            await _db.InsertAsync(recipe);
            return recipe.Id;
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

        public async Task UpdateRecipeServingSizeAsync(int recipeId, int servingSize)
        {
            await EnsureInitializedAsync();
            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe != null)
            {
                recipe.ServingSize = servingSize;
                await _db.UpdateAsync(recipe);
            }
        }

        public async Task<(double Cost, bool AllPriced)> GetRecipeCostDetailsAsync(int recipeId, int peopleCount)
        {
            try
            {
                await EnsureInitializedAsync();

                var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
                if (recipe == null) return (0, false);

                var allProducts = await GetProductsCachedAsync();
                var allIngredients = await GetIngredientsCachedAsync();
                var allAliases = await GetAliasesCachedAsync();

                var (cost, allPriced, _) = CalculateFullRecipeCost(recipe, peopleCount, allProducts, allIngredients, allAliases);
                return (cost, allPriced);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při výpočtu ceny: {ex.Message}");
                return (0, false);
            }
        }

        public async Task SetProductUnitAsync(int productId, string unit)
        {
            await EnsureInitializedAsync();
            var product = await _db.Table<LocalProduct>().Where(p => p.Id == productId).FirstOrDefaultAsync();
            if (product != null)
            {
                product.Unit = unit;
                await _db.UpdateAsync(product);
                _cachedProducts = null;
            }
        }
    }

    public class RecipeWithCost
    {
        public Recipe Recipe { get; set; } = null!;
        public double CalculatedCost { get; set; }
        public bool IsWithinBudget { get; set; }
        public bool AllIngredientsPriced { get; set; } = true;
        public bool AnyIngredientsPriced { get; set; } = true;
        public string CostColor => IsWithinBudget ? "#4CAF50" : "#F44336";
        public string BudgetStatusText => IsWithinBudget ? "Vejde se do rozpočtu!" : "Nad denní limit";
        public string CostDisplayText => (AllIngredientsPriced && CalculatedCost > 0) ? $"Cena nákupu: {CalculatedCost:N0} Kč" : "Cena nákupu: ? Kč";
    }

    public class DisplayIngredient
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AmountText { get; set; } = string.Empty;
        public string CostText { get; set; } = string.Empty;
        public double RawAmount { get; set; }
        public double CostValue { get; set; }
    }
}
