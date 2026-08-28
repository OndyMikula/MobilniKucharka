using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Classes.UserData.Bookmark;
using MobilniKucharka.Translation;

namespace MobilniKucharka.Services
{
    // Správa dočasných receptů ze SearchPage - recept zobrazený jen přes "Detail" (bez importu)
    // se uloží do DB jako dočasný (IsSearchTemp = true), aby fungovaly detail stránka, cena a
    // překlad stejně jako u trvalých receptů - ale zase zmizí, jakmile uživatel spustí nové hledání.
    // Naimportované recepty (MarkRecipeSearchTempAsync s isTemp:false) tímhle úklidem nikdy neprojdou.
    public partial class BudgetPlannerService
    {
        // Zavolat TĚSNĚ PŘED spuštěním nového hledání (viz SearchPage.OnSearchButtonClicked) -
        // smaže recepty, které minulé hledání uložilo do DB přes "Detail", ale uživatel je
        // nenaimportoval. Nikdy nemaže recepty naimportované tlačítkem "Importovat".
        public async Task DeleteSearchTempRecipesAsync()
        {
            await EnsureInitializedAsync();

            var tempRecipes = await _db.Table<Recipe>().Where(r => r.IsSearchTemp).ToListAsync();
            if (tempRecipes.Count == 0) return;

            foreach (var recipe in tempRecipes)
            {
                await _db.DeleteAsync(recipe);

                var links = await _db.Table<RecipeBookmark>().Where(rb => rb.RecipeId == recipe.Id).ToListAsync();
                foreach (var link in links)
                    await _db.DeleteAsync(link);

                // Překladová cache pro dočasný recept nemá smysl si držet navždy, když recept
                // za chvíli přestane existovat - viz RecipeTranslationCache.cs.
                var cacheEntries = await _db.Table<RecipeTranslationCache>().Where(c => c.RecipeId == recipe.Id).ToListAsync();
                foreach (var entry in cacheEntries)
                    await _db.DeleteAsync(entry);
            }
        }

        // Recept uložený ze SearchPage se označí jako dočasný (isTemp:true) po zobrazení přes
        // "Detail", nebo jako trvalý (isTemp:false) po "Importovat" - viz SearchPage.OpenRecipeAsync.
        public async Task MarkRecipeSearchTempAsync(int recipeId, bool isTemp)
        {
            await EnsureInitializedAsync();
            var recipe = await _db.Table<Recipe>().Where(r => r.Id == recipeId).FirstOrDefaultAsync();
            if (recipe != null)
            {
                recipe.IsSearchTemp = isTemp;
                await _db.UpdateAsync(recipe);
            }
        }
    }
}