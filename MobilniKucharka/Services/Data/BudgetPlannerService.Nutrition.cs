using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Services.Cache;
using MobilniKucharka.Services.Recipes;

namespace MobilniKucharka.Services.Data
{
    public partial class BudgetPlannerService
    {
        // Jednorázově přepočítá odhadnutou nutrici novým výpočtem
        public async Task<int> EnsureNutritionRecalcMigrationAsync()
        {
            await EnsureInitializedAsync();

            const string prefKey = "NutritionRecalcMigrationDone_v2";
            if (Preferences.Default.Get(prefKey, false)) return 0;

            var recipes = await _db.Table<Recipe>().Where(r => r.IsNutritionEstimated).ToListAsync();
            int fixedCount = 0;

            foreach (var recipe in recipes)
            {
                var pairs = recipe.IngredientsRaw
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split('|'))
                    .Select(parts => (Name: parts[0].Trim(), Amount: parts.ElementAtOrDefault(1)?.Trim() ?? ""))
                    .Where(p => p.Name.Length > 0)
                    .ToList();

                var (protein, carbs, fat, sugar) = NutritionEstimationService.EstimateNutrition(pairs);
                recipe.Protein = protein;
                recipe.Carbs = carbs;
                recipe.Fat = fat;
                recipe.Sugar = sugar;

                await _db.UpdateAsync(recipe);
                fixedCount++;
            }

            if (fixedCount > 0) RecipeListCache.Invalidate();
            Preferences.Default.Set(prefKey, true);
            return fixedCount;
        }
    }
}