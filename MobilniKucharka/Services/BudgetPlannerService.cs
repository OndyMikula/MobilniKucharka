using MobilniKucharka.Classes;
using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Classes.UserData.Bookmark;
using MobilniKucharka.Services.Api;
using MobilniKucharka.Translation;
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

                var recipeCount = await _db.Table<LocalProduct>().CountAsync();
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
                // Základ se skutečnými cenami (ČSÚ) - beze změny
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
                new() { Id = 12, Name_CS = "Jablka", Name_EN = "Apples", Unit = "g", PriceAverage = 0.037 },

                // Nejpoužívanější (bez ceny) - seřazeno podle skutečného využití napříč recepty
                new() { Id = 13, Name_CS = "Sůl", Name_EN = "Salt", Unit = "g" },
                new() { Id = 14, Name_CS = "Cukr", Name_EN = "Sugar", Unit = "g" },
                new() { Id = 15, Name_CS = "Mléko", Name_EN = "Milk", Unit = "ml" },
                new() { Id = 16, Name_CS = "Voda", Name_EN = "Water", Unit = "g" },
                new() { Id = 17, Name_CS = "Olej", Name_EN = "Oil", Unit = "ml" },
                new() { Id = 18, Name_CS = "Pepř", Name_EN = "Pepper", Unit = "g" },
                new() { Id = 19, Name_CS = "Česnek", Name_EN = "Garlic", Unit = "ks" },
                new() { Id = 20, Name_CS = "Cibule", Name_EN = "Onion", Unit = "ks" },
                new() { Id = 21, Name_CS = "Mouka", Name_EN = "Flour", Unit = "ml" },
                new() { Id = 22, Name_CS = "Kvasnice", Name_EN = "Yeast", Unit = "g" },
                new() { Id = 23, Name_CS = "Sezamová semínka", Name_EN = "Sesame seeds", Unit = "ml" },
                new() { Id = 24, Name_CS = "Jehněčí kýta", Name_EN = "Lamb Leg", Unit = "g" },
                new() { Id = 25, Name_CS = "Olivový olej", Name_EN = "Olive oil", Unit = "ml" },
                new() { Id = 26, Name_CS = "Mletý kmín", Name_EN = "Ground Cumin", Unit = "g" },
                new() { Id = 27, Name_CS = "Ocet z bílého vína", Name_EN = "White Wine Vinegar", Unit = "ml" },
                new() { Id = 28, Name_CS = "Mrkev", Name_EN = "Carrot", Unit = "ml" },
                new() { Id = 29, Name_CS = "Jarní cibulky", Name_EN = "Spring onions", Unit = "ks" },
                new() { Id = 30, Name_CS = "Bílé zelí", Name_EN = "White Cabbage", Unit = "g" },
                new() { Id = 31, Name_CS = "Sladké papričky Peppadew", Name_EN = "Sweet Peppadew Peppers", Unit = "g" },
                new() { Id = 32, Name_CS = "Majonéza", Name_EN = "Mayonnaise", Unit = "g" },
                new() { Id = 33, Name_CS = "Pita chléb", Name_EN = "Pita Bread", Unit = "g" },
                new() { Id = 34, Name_CS = "Tortilla", Name_EN = "Tortilla", Unit = "g" },
                new() { Id = 35, Name_CS = "Kuřecí prsa", Name_EN = "Chicken breast", Unit = "g" },
                new() { Id = 36, Name_CS = "Paprika (na koření)", Name_EN = "Paprika (spice)", Unit = "g" },
                new() { Id = 37, Name_CS = "Okurka", Name_EN = "Cucumber", Unit = "g" },
                new() { Id = 38, Name_CS = "Listový salát", Name_EN = "Leaf lettuce", Unit = "g" },
                new() { Id = 39, Name_CS = "Řecký jogurt (nebo bílý jogurt)", Name_EN = "Greek yogurt (or plain yogurt)", Unit = "g" },
                new() { Id = 40, Name_CS = "Rýžové nudle", Name_EN = "Rice Noodles", Unit = "g" },
                new() { Id = 41, Name_CS = "Sezamový olej", Name_EN = "Sesame oil", Unit = "ml" },
                new() { Id = 42, Name_CS = "Červená chilli paprička", Name_EN = "Red chili pepper", Unit = "g" },
                new() { Id = 43, Name_CS = "Zázvor", Name_EN = "Ginger", Unit = "g" },
                new() { Id = 44, Name_CS = "Krevety", Name_EN = "Shrimp", Unit = "g" },
                new() { Id = 45, Name_CS = "Sójová omáčka", Name_EN = "Soy sauce", Unit = "ml" },
                new() { Id = 46, Name_CS = "Koriandr", Name_EN = "Coriander", Unit = "g" },
                new() { Id = 47, Name_CS = "Máta", Name_EN = "Mint", Unit = "g" },
                new() { Id = 48, Name_CS = "Limetka", Name_EN = "Lime", Unit = "ks" },
                new() { Id = 49, Name_CS = "Arašídy", Name_EN = "Peanuts", Unit = "g" },
                new() { Id = 50, Name_CS = "Kuře", Name_EN = "Chicken", Unit = "g" },
                new() { Id = 51, Name_CS = "Rajčata", Name_EN = "Tomatoes", Unit = "g" },
                new() { Id = 52, Name_CS = "Zázvorová pasta", Name_EN = "Ginger Paste", Unit = "ml" },
                new() { Id = 53, Name_CS = "Rostlinný olej", Name_EN = "Vegetable oil", Unit = "g" },
                new() { Id = 54, Name_CS = "Semena kmínu", Name_EN = "Cumin seeds", Unit = "ml" },
                new() { Id = 55, Name_CS = "Koriandrová semínka", Name_EN = "Coriander seeds", Unit = "ml" },
                new() { Id = 56, Name_CS = "Kurkuma v prášku", Name_EN = "Turmeric Powder", Unit = "ml" },
                new() { Id = 57, Name_CS = "Chilli v prášku", Name_EN = "Chili powder", Unit = "ml" },
                new() { Id = 58, Name_CS = "Zelené chilli", Name_EN = "Green chili peppers", Unit = "ks" },
                new() { Id = 59, Name_CS = "Jogurt", Name_EN = "Yogurt", Unit = "ml" },
                new() { Id = 60, Name_CS = "Smetana", Name_EN = "Cream", Unit = "ml" },
                new() { Id = 61, Name_CS = "Pískavice řecké seno", Name_EN = "Fenugreek", Unit = "ml" },
                new() { Id = 62, Name_CS = "Garam masala", Name_EN = "Garam masala", Unit = "ml" },
                new() { Id = 63, Name_CS = "Slunečnicový olej", Name_EN = "Sunflower oil", Unit = "ml" },
                new() { Id = 64, Name_CS = "Maliny", Name_EN = "Raspberries", Unit = "g" },
                new() { Id = 65, Name_CS = "Borůvky", Name_EN = "Blueberries", Unit = "g" },
                new() { Id = 66, Name_CS = "Pohanka", Name_EN = "Buckwheat", Unit = "ml" },
                new() { Id = 67, Name_CS = "Mleté hovězí maso", Name_EN = "Ground beef", Unit = "g" },
                new() { Id = 68, Name_CS = "Kukuřičná kaše", Name_EN = "Corn porridge", Unit = "ml" },
                new() { Id = 69, Name_CS = "Petržel", Name_EN = "Parsley", Unit = "g" },
                new() { Id = 70, Name_CS = "Rafinovaná mouka", Name_EN = "Refined flour", Unit = "g" },
                new() { Id = 71, Name_CS = "Vanilkový cukr", Name_EN = "Vanilla sugar", Unit = "g" },
                new() { Id = 72, Name_CS = "Rum", Name_EN = "Rum", Unit = "ml" },
                new() { Id = 73, Name_CS = "Polohrubá mouka", Name_EN = "Semi-coarse flour", Unit = "g" },
                new() { Id = 74, Name_CS = "Krupicový cukr", Name_EN = "Granulated sugar", Unit = "g" },

                // Základní suroviny chybějící v původním seznamu
                new() { Id = 75, Name_CS = "Skořice", Name_EN = "Cinnamon", Unit = "g" },
                new() { Id = 76, Name_CS = "Bazalka", Name_EN = "Basil", Unit = "g" },
                new() { Id = 77, Name_CS = "Oregano", Name_EN = "Oregano", Unit = "g" },
                new() { Id = 78, Name_CS = "Tymián", Name_EN = "Thyme", Unit = "g" },
                new() { Id = 79, Name_CS = "Sýr", Name_EN = "Cheese", Unit = "g" },
                new() { Id = 80, Name_CS = "Šunka", Name_EN = "Ham", Unit = "g" },
                new() { Id = 81, Name_CS = "Kuřecí vývar", Name_EN = "Chicken broth", Unit = "ml" },
                new() { Id = 82, Name_CS = "Banán", Name_EN = "Banana", Unit = "ks" },

                // Ostatní - zbytek zdrojového seznamu, s opravenými překlady a bez duplicit
                new() { Id = 83, Name_CS = "Bílá fazole", Name_EN = "White beans", Unit = "g" },
                new() { Id = 84, Name_CS = "Bobkový list", Name_EN = "Bay leaf", Unit = "ks" },
                new() { Id = 85, Name_CS = "Brokolice", Name_EN = "Broccoli", Unit = "g" },
                new() { Id = 86, Name_CS = "Citron", Name_EN = "Lemon", Unit = "g" },
                new() { Id = 87, Name_CS = "Černý pepř", Name_EN = "Black pepper", Unit = "ml" },
                new() { Id = 88, Name_CS = "Červená paprika", Name_EN = "Red bell pepper", Unit = "g" },
                new() { Id = 89, Name_CS = "Červené chilli", Name_EN = "Red Chilli", Unit = "g" },
                new() { Id = 90, Name_CS = "Čirý med", Name_EN = "Clear honey", Unit = "ml" },
                new() { Id = 91, Name_CS = "Debrecinka šunka", Name_EN = "Debrecen Ham", Unit = "g" },
                new() { Id = 92, Name_CS = "Fazole Haricot", Name_EN = "Haricot Beans", Unit = "g" },
                new() { Id = 93, Name_CS = "Hnědý cukr", Name_EN = "Brown sugar", Unit = "g" },
                new() { Id = 94, Name_CS = "Houby", Name_EN = "Mushrooms", Unit = "ml" },
                new() { Id = 95, Name_CS = "Hovězí maso", Name_EN = "Beef", Unit = "g" },
                new() { Id = 96, Name_CS = "Hovězí vývar", Name_EN = "Beef broth", Unit = "ml" },
                new() { Id = 97, Name_CS = "Hovězí vývarový koncentrát", Name_EN = "Beef Bouillon Concentrate", Unit = "ks" },
                new() { Id = 98, Name_CS = "Hřebíček", Name_EN = "Clove", Unit = "ml" },
                new() { Id = 99, Name_CS = "Husí tuk", Name_EN = "Goose Fat", Unit = "ml" },
                new() { Id = 100, Name_CS = "Chilli", Name_EN = "Chilli", Unit = "g" },
                new() { Id = 101, Name_CS = "Chléb", Name_EN = "Bread", Unit = "g" },
                new() { Id = 102, Name_CS = "Jamón ibérico", Name_EN = "Iberian ham", Unit = "g" },
                new() { Id = 103, Name_CS = "Kabse Spice", Name_EN = "Kabse Spice", Unit = "g" },
                new() { Id = 104, Name_CS = "Kakao", Name_EN = "Cocoa", Unit = "ml" },
                new() { Id = 105, Name_CS = "Kakaový prášek", Name_EN = "Cocoa powder", Unit = "ml" },
                new() { Id = 106, Name_CS = "Kardamom", Name_EN = "Cardamom", Unit = "ml" },
                new() { Id = 107, Name_CS = "Kečup", Name_EN = "Ketchup", Unit = "ml" },
                new() { Id = 108, Name_CS = "Klíčky fazolí", Name_EN = "Bean Sprouts", Unit = "ml" },
                new() { Id = 109, Name_CS = "Kokos", Name_EN = "Coconut", Unit = "ks" },
                new() { Id = 110, Name_CS = "Koření", Name_EN = "Spices", Unit = "ml" },
                new() { Id = 111, Name_CS = "Košerová sůl", Name_EN = "Kosher Salt", Unit = "g" },
                new() { Id = 112, Name_CS = "Kukuřice cukrová", Name_EN = "Sweetcorn", Unit = "g" },
                new() { Id = 113, Name_CS = "Kukuřičná mouka", Name_EN = "Corn flour", Unit = "ml" },
                new() { Id = 114, Name_CS = "Listy medvědího česneku", Name_EN = "Wild Garlic Leaves", Unit = "g" },
                new() { Id = 115, Name_CS = "Sýr Manchego", Name_EN = "Manchego Cheese", Unit = "g" },
                new() { Id = 116, Name_CS = "Margarín", Name_EN = "Margarine", Unit = "g" },
                new() { Id = 117, Name_CS = "Mletý česnek", Name_EN = "Minced garlic", Unit = "ml" },
                new() { Id = 118, Name_CS = "Mořské ulity", Name_EN = "Seashells", Unit = "ml" },
                new() { Id = 119, Name_CS = "Muškátový oříšek", Name_EN = "Nutmeg", Unit = "g" },
                new() { Id = 120, Name_CS = "Mušle", Name_EN = "Mussels", Unit = "ml" },
                new() { Id = 121, Name_CS = "Nesolené máslo", Name_EN = "Unsalted butter", Unit = "ml" },
                new() { Id = 122, Name_CS = "Nudle", Name_EN = "Noodles", Unit = "ml" },
                new() { Id = 123, Name_CS = "Ocet z červeného vína", Name_EN = "Red Wine Vinegar", Unit = "ml" },
                new() { Id = 124, Name_CS = "Parmazán", Name_EN = "Parmesan", Unit = "g" },
                new() { Id = 125, Name_CS = "Prášek do pečiva", Name_EN = "Baking powder", Unit = "g" },
                new() { Id = 126, Name_CS = "Pytel hluboce mražených hranolek", Name_EN = "A bag of deep-frozen french fries", Unit = "g" },
                new() { Id = 127, Name_CS = "Rajčatové pyré", Name_EN = "Tomato Puree", Unit = "g" },
                new() { Id = 128, Name_CS = "Rajčatový protlak", Name_EN = "Tomato paste", Unit = "ml" },
                new() { Id = 129, Name_CS = "Rozinky", Name_EN = "Raisins", Unit = "g" },
                new() { Id = 130, Name_CS = "Rozmarýn", Name_EN = "Rosemary", Unit = "g" },
                new() { Id = 131, Name_CS = "Rýže", Name_EN = "Rice", Unit = "g" },
                new() { Id = 132, Name_CS = "Rýže basmati", Name_EN = "Basmati rice", Unit = "g" },
                new() { Id = 133, Name_CS = "Rýžový ocet", Name_EN = "Rice vinegar", Unit = "ml" },
                new() { Id = 134, Name_CS = "Saké", Name_EN = "Sake", Unit = "g" },
                new() { Id = 135, Name_CS = "Semena fenyklu", Name_EN = "Fennel Seeds", Unit = "ml" },
                new() { Id = 136, Name_CS = "Slanina", Name_EN = "Bacon", Unit = "ks" },
                new() { Id = 137, Name_CS = "Směs na bílý chléb", Name_EN = "White Bread Mix", Unit = "g" },
                new() { Id = 138, Name_CS = "Smetana na vaření", Name_EN = "Cooking cream", Unit = "g" },
                new() { Id = 139, Name_CS = "Solené hovězí", Name_EN = "Corned Beef", Unit = "ml" },
                new() { Id = 140, Name_CS = "Strouhanka", Name_EN = "Breadcrumbs", Unit = "g" },
                new() { Id = 141, Name_CS = "Stroužky česneku", Name_EN = "Garlic cloves", Unit = "ks" },
                new() { Id = 142, Name_CS = "Škrob", Name_EN = "Starch", Unit = "ml" },
                new() { Id = 143, Name_CS = "Šunkové Tortelini v pytlíku", Name_EN = "Ham Tortellini in a Bag", Unit = "g" },
                new() { Id = 144, Name_CS = "Univerzální mouka", Name_EN = "All-Purpose Flour", Unit = "ml" },
                new() { Id = 145, Name_CS = "Ústřicová omáčka", Name_EN = "Oyster Sauce", Unit = "ml" },
                new() { Id = 146, Name_CS = "Vajíčkový žloutek", Name_EN = "Egg yolk", Unit = "g" },
                new() { Id = 147, Name_CS = "Vepřové kotlety", Name_EN = "Pork Chops", Unit = "ks" },
                new() { Id = 148, Name_CS = "Vepřové maso", Name_EN = "Pork", Unit = "g" },
                new() { Id = 149, Name_CS = "Vodní kaštany", Name_EN = "Water chestnuts", Unit = "g" },
                new() { Id = 150, Name_CS = "Worcesterská omáčka", Name_EN = "Worcestershire sauce", Unit = "ml" },
                new() { Id = 151, Name_CS = "Zelená paprika", Name_EN = "Green bell pepper", Unit = "g" },
                new() { Id = 152, Name_CS = "Zeleninový vývar", Name_EN = "Vegetable Broth", Unit = "ml" },
            };

            foreach (var prod in products)
            {
                await _db.InsertOrReplaceAsync(prod);
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

        private static bool MatchesUserDiets(Recipe recipe, List<string> userDiets)
        {
            if (userDiets.Count == 0) return true;

            foreach (var diet in userDiets)
            {
                bool compatible = diet switch
                {
                    "Vegetarian" => DietaryAnalysisService.IsVegetarianCompatible(recipe.IngredientsRaw),
                    "Vegan" => DietaryAnalysisService.IsVeganCompatible(recipe.IngredientsRaw),
                    "LactoseFree" => DietaryAnalysisService.IsLactoseFreeCompatible(recipe.IngredientsRaw),
                    _ => true
                };
                if (!compatible) return false;
            }
            return true;
        }

        private static bool MatchesUserEquipment(Recipe recipe, List<string> userEquipment)
        {
            if (userEquipment.Count == 0) return true;
            return recipe.RequiredEquipment.All(a => userEquipment.Contains(a));
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
                    if (!MatchesUserDiets(recipe, userDiets))
                        continue;

                    if (!MatchesUserEquipment(recipe, userEquipment))
                        continue;

                    // Doplní jméno (a kroky) do aktuálního jazyka aplikace, pokud ještě chybí - díky cache uvnitř
                    // EnsureRecipeLanguageAsync se DeepL zavolá jen jednou za (recept, jazyk) navždy; další
                    // zobrazení seznamu je pak jen levná kontrola v DB, ne nové volání API.
                    var displayRecipe = await EnsureRecipeLanguageAsync(recipe.Id, recipe) ?? recipe;

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
                        MatchesUserDiets(r, userDiets) &&
                        MatchesUserEquipment(r, userEquipment)
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
                    var displayRecipe = await EnsureRecipeLanguageAsync(match.Id, match) ?? match;

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
                // se tu doplní do aktuálního jazyka aplikace, pokud ještě nebyl zobrazen přes
                // RecipeDetailPage. Díky tomu se i "Vytvořené recepty" (kam Import odkládá recepty)
                // zobrazují správně přeložené. Cache uvnitř EnsureRecipeLanguageAsync zajistí, že se
                // DeepL nezavolá znovu, pokud už překlad existuje.
                var displayRecipes = new List<Recipe>();
                foreach (var recipe in matchedRecipes)
                {
                    displayRecipes.Add(await EnsureRecipeLanguageAsync(recipe.Id, recipe) ?? recipe);
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

        public async Task<List<string>> GetAllProductNameSuggestionsAsync()
        {
            await EnsureInitializedAsync();
            var products = await _db.Table<LocalProduct>().ToListAsync();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in products)
            {
                if (!string.IsNullOrWhiteSpace(p.Name_CS)) names.Add(p.Name_CS);
                if (!string.IsNullOrWhiteSpace(p.Name_EN)) names.Add(p.Name_EN);
            }
            return [.. names.OrderBy(n => n)];
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

        public async Task<LocalProduct> GetOrCreateLocalProductByNameAsync(string name, string suggestedUnit = "g", string? sourceLang = null)
        {
            await EnsureInitializedAsync();
            string trimmed = name.Trim();
            string normalizedTyped = TextNormalizationHelper.NormalizeForComparison(trimmed);
            string lang = string.IsNullOrWhiteSpace(sourceLang) ? Preferences.Default.Get("AppLanguageCode", "cs") : sourceLang;

            var allAliases = await _db.Table<LocalProductAlias>().ToListAsync();
            var alias = allAliases.FirstOrDefault(a => TextNormalizationHelper.NormalizeForComparison(a.Alias) == normalizedTyped);

            if (alias != null)
            {
                var aliasedProduct = await _db.Table<LocalProduct>().Where(p => p.Id == alias.ProductId).FirstOrDefaultAsync();
                if (aliasedProduct != null) return aliasedProduct;
            }

            var allProducts = await _db.Table<LocalProduct>().ToListAsync();
            var existing = allProducts.FirstOrDefault(p =>
                TextNormalizationHelper.NormalizeForComparison(p.Name_CS) == normalizedTyped ||
                TextNormalizationHelper.NormalizeForComparison(p.Name_EN) == normalizedTyped);

            // Stejné slovo před závorkou ("Paprika (koření)" vs "Paprika (na koření)") - jen když
            // OBĚ strany mají závorku, ať se holé "Paprika" (zelenina) neslije s "Paprika
            // (koření)" (drť) - to jsou reálně různé suroviny.
            if (existing == null)
            {
                var (typedBase, typedHasParen) = TextNormalizationHelper.SplitBaseAndParenthetical(trimmed);
                if (typedHasParen && typedBase.Length > 0)
                {
                    existing = allProducts.FirstOrDefault(p =>
                    {
                        var (csBase, csHasParen) = TextNormalizationHelper.SplitBaseAndParenthetical(p.Name_CS);
                        if (csHasParen && csBase == typedBase) return true;
                        var (enBase, enHasParen) = TextNormalizationHelper.SplitBaseAndParenthetical(p.Name_EN);
                        return enHasParen && enBase == typedBase;
                    });
                }
            }

            if (existing != null)
            {
                // Jiný zápis stejného slova (diakritika/velikost písmen) - uložit jako alias pro
                // příště, ale jen pokud přesně tenhle zápis ještě není zaznamenaný.
                bool exactMatchAlready = string.Equals(existing.Name_CS, trimmed, StringComparison.Ordinal) ||
                                         string.Equals(existing.Name_EN, trimmed, StringComparison.Ordinal) ||
                                         allAliases.Any(a => string.Equals(a.Alias, trimmed, StringComparison.Ordinal) && a.ProductId == existing.Id);
                if (!exactMatchAlready)
                    await _db.InsertAsync(new LocalProductAlias { Alias = trimmed, ProductId = existing.Id });

                return existing;
            }

            // Nenalezeno - zkusit přeložit do druhého jazyka a najít stejnou surovinu tam (např.
            // "Sugar" napsané poprvé anglicky, ale "Cukr" už existuje) - ať nevznikne duplicita jen
            // proto, že recept je v jiném jazyce než existující záznam.
            string otherLang = lang == "cs" ? "en" : "cs";
            string capitalizedTyped = Capitalize(trimmed);
            string? translated = IngredientTranslationOverrides.TryGet(trimmed, lang)
                ?? await TranslationService.TranslateAsync(trimmed, otherLang, lang);
            string capitalizedTranslated = string.IsNullOrWhiteSpace(translated) ? capitalizedTyped : Capitalize(translated);

            if (!string.IsNullOrWhiteSpace(translated))
            {
                string normalizedTranslated = TextNormalizationHelper.NormalizeForComparison(translated);
                var crossMatch = allProducts.FirstOrDefault(p =>
                    TextNormalizationHelper.NormalizeForComparison(p.Name_CS) == normalizedTranslated ||
                    TextNormalizationHelper.NormalizeForComparison(p.Name_EN) == normalizedTranslated);

                if (crossMatch != null)
                {
                    await _db.InsertAsync(new LocalProductAlias { Alias = trimmed, ProductId = crossMatch.Id });
                    return crossMatch;
                }
            }

            var newProduct = new LocalProduct
            {
                Name_CS = lang == "cs" ? capitalizedTyped : capitalizedTranslated,
                Name_EN = lang == "cs" ? capitalizedTranslated : capitalizedTyped,
                Unit = suggestedUnit,
                PriceAverage = 0
            };

            await _db.InsertAsync(newProduct);
            _cachedProducts = null;
            return newProduct;
        }

        private static string Capitalize(string text)
        {
            text = text.Trim();
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
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
        public static readonly string[] ProtectedBookmarkNames = ["Oblíbené", "Vytvořené recepty", "Vyhledané recepty", "Koncepty"];

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
        // zasažených starým bugem, kdy uživatelem vybraný obrázek zmizel po aktualizaci aplikace (viz
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
                EquipmentJson = "[]",
                DietaryFlagsJson = JsonSerializer.Serialize(GuessDietFlags(mealDbRecipe.Category)),
                IngredientsRaw = string.Join("\n", mealDbRecipe.Ingredients.Select(i => $"{i.Name}|{i.Measure}")),
                ContentLanguage = "en",
                DescriptionLanguage = "en",
                SourceUrl = mealDbRecipe.SourceUrl,
                ServingSize = 0,
                RequiredEquipment = RequiredEquipmentAnalysisService.InferRequiredEquipment(mealDbRecipe.Instructions)
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
            string normalizedTyped = TextNormalizationHelper.NormalizeForComparison(name);

            var alias = allAliases.FirstOrDefault(a => TextNormalizationHelper.NormalizeForComparison(a.Alias) == normalizedTyped);
            if (alias != null)
            {
                var aliasedProduct = allProducts.FirstOrDefault(p => p.Id == alias.ProductId);
                if (aliasedProduct != null) return aliasedProduct;
            }

            return allProducts.FirstOrDefault(p =>
                TextNormalizationHelper.NormalizeForComparison(p.Name_CS) == normalizedTyped ||
                TextNormalizationHelper.NormalizeForComparison(p.Name_EN) == normalizedTyped);
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

        public async Task<bool> RenameProductAsync(int productId, string newNameCs, string newNameEn)
        {
            await EnsureInitializedAsync();
            var product = await _db.Table<LocalProduct>().Where(p => p.Id == productId).FirstOrDefaultAsync();
            if (product == null) return false;

            product.Name_CS = newNameCs.Trim();
            product.Name_EN = newNameEn.Trim();
            await _db.UpdateAsync(product);
            _cachedProducts = null;
            return true;
        }

        // Vrátí počet receptů, které surovinu používají - ať uživatel před smazáním ví, co se
        // dotkne (stejný princip jako u DeleteBookmarkAsync, jen tady se smazání týká i receptů).
        public async Task<int> CountRecipesUsingProductAsync(int productId)
        {
            await EnsureInitializedAsync();
            var links = await _db.Table<RecipeIngredient>().Where(ri => ri.ProductId == productId).ToListAsync();
            return links.Select(ri => ri.RecipeId).Distinct().Count();
        }

        public async Task DeleteProductAsync(int productId)
        {
            await EnsureInitializedAsync();

            var product = await _db.Table<LocalProduct>().Where(p => p.Id == productId).FirstOrDefaultAsync();
            if (product != null) await _db.DeleteAsync(product);

            var links = await _db.Table<RecipeIngredient>().Where(ri => ri.ProductId == productId).ToListAsync();
            foreach (var link in links) await _db.DeleteAsync(link);

            var aliases = await _db.Table<LocalProductAlias>().Where(a => a.ProductId == productId).ToListAsync();
            foreach (var alias in aliases) await _db.DeleteAsync(alias);

            _cachedProducts = null;
        }

        public async Task<LocalProduct> CreateProductAsync(string nameCs, string nameEn, string unit)
        {
            await EnsureInitializedAsync();

            var product = new LocalProduct
            {
                Name_CS = nameCs.Trim(),
                Name_EN = string.IsNullOrWhiteSpace(nameEn) ? nameCs.Trim() : nameEn.Trim(),
                Unit = unit,
                PriceAverage = 0
            };

            await _db.InsertAsync(product);
            _cachedProducts = null;
            return product;
        }

        // Ruční sloučení 2+ vybraných surovin do jedné (Nastavení > Suroviny) - kanonický je vždy
        // první v seznamu (uživatel ho vybírá explicitně, na rozdíl od automatického MergeDuplicate-
        // ProductsAsync, kde se kanonický odhaduje podle počtu použití).
        public async Task<int> MergeSelectedProductsAsync(int canonicalId, List<int> duplicateIds)
        {
            await EnsureInitializedAsync();

            var canonical = await _db.Table<LocalProduct>().Where(p => p.Id == canonicalId).FirstOrDefaultAsync();
            if (canonical == null) return 0;

            int merged = 0;
            foreach (var dupId in duplicateIds)
            {
                if (dupId == canonicalId) continue;
                var duplicate = await _db.Table<LocalProduct>().Where(p => p.Id == dupId).FirstOrDefaultAsync();
                if (duplicate == null) continue;

                await MergeProductPairAsync(canonical, duplicate);
                merged++;
            }

            _cachedProducts = null;
            _cachedAliases = null;
            return merged;
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