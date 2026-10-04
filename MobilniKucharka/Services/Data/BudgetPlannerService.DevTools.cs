using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Services.Cache;

namespace MobilniKucharka.Services.Data
{
    // Vývojářské nástroje pro zátěžové testování (virtualizovaný seznam při velkém počtu
    // receptů) - nikdy se nespouští automaticky, jen ručně z dev-tools řádku na RecipesPage.
    // ExternalSourceId s prefixem jednoznačně odlišuje tyhle recepty od skutečných uživatelových
    // (ty mají ExternalSourceId vždy prázdné) - Vrátit zpět tedy nikdy nesmaže nic skutečného.
    public partial class BudgetPlannerService
    {
        private const string TestRecipePrefix = "devtest_";

        public async Task GenerateTestRecipesAsync(int count)
        {
            await EnsureInitializedAsync();

            for (int i = 0; i < count; i++)
            {
                var recipe = new Recipe
                {
                    Name_CS = $"Testovací recept {i + 1}",
                    Name_EN = $"Test recipe {i + 1}",
                    ExternalSourceId = $"{TestRecipePrefix}{Guid.NewGuid():N}",
                    Category = "Vytvořené recepty",
                    ContentLanguage = "cs",
                    DescriptionLanguage = "cs",
                    IngredientsRaw = "Testovací surovina|100 g",
                    StepsJson_CS = "[\"Testovací krok.\"]",
                    StepsJson_EN = "[\"Test step.\"]",
                    ServingSize = 2,
                    PrepTime = 10,
                    Protein = 5,
                    Carbs = 10,
                    Fat = 2,
                    Sugar = 1,
                    IsNutritionEstimated = true
                };

                await _db.InsertAsync(recipe);
                await AddRecipeToCategoryAsync(recipe.Id, "Vytvořené recepty");
            }

            RecipeListCache.Invalidate();
        }

        public async Task<int> DeleteTestRecipesAsync()
        {
            await EnsureInitializedAsync();

            var testRecipes = await _db.Table<Recipe>().Where(r => r.ExternalSourceId.StartsWith(TestRecipePrefix)).ToListAsync();

            foreach (var recipe in testRecipes)
                await DeleteRecipeAsync(recipe.Id);

            RecipeListCache.Invalidate();
            return testRecipes.Count;
        }
    }
}