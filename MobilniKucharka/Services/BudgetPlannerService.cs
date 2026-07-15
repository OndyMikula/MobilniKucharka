using SQLite;
using MobilniKucharka.Classes;
using System.Text.Json;
using MobilniKucharka.Classes.Recipe;

namespace MobilniKucharka.Services
{
    public class BudgetPlannerService
    {
        private readonly SQLiteAsyncConnection _db;

        public BudgetPlannerService(string dbPath)
        {
            _db = new SQLiteAsyncConnection(dbPath);
        }

        public async Task<List<RecipeWithCost>> GetPlanAsync()
        {
            // 1. Načtení uživatelských preferencí z paměti
            string shopColumnName = Preferences.Default.Get("ShopColName", "PriceLidl");
            int peopleCount = Preferences.Default.Get("PeopleCount", 2);
            double weeklyBudget = Preferences.Default.Get("WeeklyBudget", 2000.0);

            string dietsStr = Preferences.Default.Get("UserDiets", "");
            var userDiets = string.IsNullOrEmpty(dietsStr) ? [] : dietsStr.Split(',').ToList();

            string appliancesStr = Preferences.Default.Get("UserAppliances", "Trouba,Sporák,Konvice,Mikrovlnka");
            var userAppliances = appliancesStr.Split(',').ToList();

            // 2. Načtení dat z SQLite
            var allRecipes = await _db.Table<Recipe>().ToListAsync();
            var allProducts = await _db.Table<LocalProduct>().ToListAsync();
            var allIngredients = await _db.Table<RecipeIngredient>().ToListAsync();

            var matchingRecipes = new List<RecipeWithCost>();

            foreach (var recipe in allRecipes)
            {
                // --- FILTR 1: KUCHYŇSKÉ SPOTŘEBIČE ---
                // Pokud recept vyžaduje nástroj/spotřebič, který uživatel nemá, recept přeskočíme.
                bool hasRequiredAppliances = true;
                foreach (var requiredAppliance in recipe.Tools)
                {
                    if (!userAppliances.Contains(requiredAppliance))
                    {
                        hasRequiredAppliances = false;
                        break;
                    }
                }
                if (!hasRequiredAppliances) continue;

                // Načtení surovin patřících k tomuto receptu
                var ingredientsForRecipe = allIngredients.Where(x => x.RecipeId == recipe.Id).ToList();

                double recipeTotalCost = 0;
                bool isDietOkay = true;

                // Projdeme všechny ingredience v receptu
                foreach (var ing in ingredientsForRecipe)
                {
                    // Najdeme konkrétní produkt v našem lokálním ceníku
                    var product = allProducts.FirstOrDefault(p => p.Id == ing.ProductId);
                    if (product == null) continue;

                    // --- FILTR 2: DIETNÍ OMEZENÍ ---
                    // Pokud má uživatel nastavené diety, produkt je musí splňovat
                    if (userDiets.Contains("Vegan") && !product.IsVegan) isDietOkay = false;
                    if (userDiets.Contains("Vegetarian") && !product.IsVegetarian) isDietOkay = false;
                    if (userDiets.Contains("LactoseFree") && !product.IsLactoseFree) isDietOkay = false;

                    if (!isDietOkay) break;

                    // --- VÝPOČET CENY ---
                    // Zjistíme cenu produktu v preferovaném obchodě
                    double pricePerUnit = GetProductPriceForShop(product, shopColumnName);

                    // Vypočítáme celkové množství pro zadaný počet lidí
                    double totalAmountNeeded = ing.AmountPerPerson * peopleCount;

                    // Připočítáme cenu suroviny (množství * cena za jednotku)
                    recipeTotalCost += totalAmountNeeded * pricePerUnit;
                }

                if (!isDietOkay) continue;

                // Pokud recept prošel všemi filtry, přidáme ho do seznamu i s vypočítanou cenou
                matchingRecipes.Add(new RecipeWithCost
                {
                    Recipe = recipe,
                    CalculatedCost = Math.Round(recipeTotalCost, 1),
                    IsWithinBudget = recipeTotalCost <= (weeklyBudget / 7.0) // porovnáváme s denním podílem budgetu
                });
            }

            // Vrátíme recepty seřazené od nejlevnějších
            return [.. matchingRecipes.OrderBy(r => r.CalculatedCost)];
        }

        private double GetProductPriceForShop(LocalProduct product, string shopCol)
        {
            return shopCol switch
            {
                "PriceLidl" => product.PriceLidl,
                "PriceKaufland" => product.PriceKaufland,
                "PriceAlbert" => product.PriceAlbert,
                "PriceTesco" => product.PriceTesco,
                "PriceBilla" => product.PriceBilla,
                "PricePenny" => product.PricePenny,
                _ => product.PriceLidl
            };
        }

        public async Task<List<DisplayIngredient>> GetIngredientsForRecipeAsync(int recipeId, int peopleCount, string shopColumn)
        {
            var allProducts = await _db.Table<LocalProduct>().ToListAsync();
            // Vytáhneme vazby ingrediencí pouze pro tento konkrétní recept
            var recipeIngredients = await _db.Table<RecipeIngredient>().Where(x => x.RecipeId == recipeId).ToListAsync();

            var displayList = new List<DisplayIngredient>();
            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");

            foreach (var ing in recipeIngredients)
            {
                var product = allProducts.FirstOrDefault(p => p.Id == ing.ProductId);
                if (product == null) continue;

                // Přepočet množství podle počtu osob
                double totalAmount = ing.AmountPerPerson * peopleCount;

                // Výpočet ceny v preferovaném obchodě
                double pricePerUnit = GetProductPriceForShop(product, shopColumn);
                double totalCost = Math.Round(totalAmount * pricePerUnit, 0);

                displayList.Add(new DisplayIngredient
                {
                    Name = currentLang == "cs" ? product.Name_CS : product.Name_EN,
                    AmountText = $"{totalAmount:G29} {product.Unit}", // G29 formát odstraní přebytečné nuly za desetinnou čárkou
                    CostText = totalCost > 0 ? $"{totalCost:N0} Kč" : "Zdarma/Doma" // Pokud je cena nulová (např. sůl, voda)
                });
            }

            return displayList;
        }
    }

    // Pomocná přepravní třída (DTO) pro UI
    public class RecipeWithCost
    {
        public Recipe Recipe { get; set; } = null!;
        public double CalculatedCost { get; set; }
        public bool IsWithinBudget { get; set; }

        // Nové vlastnosti pro snadné zobrazení v XAML:
        public string CostColor => IsWithinBudget ? "#4CAF50" : "#F44336"; // Zelená vs Červená
        public string BudgetStatusText => IsWithinBudget ? "Vejde se do rozpočtu!" : "Nad denní limit";
    }

    // Pomocná třída pro zobrazení surovin v UI 
    public class DisplayIngredient
    {
        public string Name { get; set; } = string.Empty;
        public string AmountText { get; set; } = string.Empty;
        public string CostText { get; set; } = string.Empty;
    }
}