using MobilniKucharka.Services.Data;
using MobilniKucharka.Services.Recipes;
using System.Text.Json;

namespace MobilniKucharka.Services
{
    public partial class BudgetPlannerService
    {
        private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(6);
        private bool _isSearchCacheReady;

        private async Task EnsureSearchCacheReadyAsync()
        {
            if (_isSearchCacheReady) return;
            await _db.CreateTableAsync<SearchResultCacheEntry>();
            _isSearchCacheReady = true;
        }

        // Zrychluje opakované stejné hledání (stejný dotaz, stejný filtr diet) - šetří volání
        // MealDB i Spoonacular. Jen na zařízení - bez backendu appka nemá jak cache sdílet napříč
        // uživateli, ale pořád snižuje celkový počet volání od každého jednotlivého uživatele.
        public async Task<List<ExternalRecipeSearchResult>?> GetCachedSearchResultsAsync(string queryKey)
        {
            await EnsureSearchCacheReadyAsync();

            var entry = await _db.Table<SearchResultCacheEntry>().Where(c => c.QueryKey == queryKey).FirstOrDefaultAsync();
            if (entry == null) return null;
            if (DateTime.UtcNow - entry.CachedAtUtc > SearchCacheTtl) return null;

            try
            {
                return JsonSerializer.Deserialize<List<ExternalRecipeSearchResult>>(entry.ResultsJson);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async Task SaveSearchResultsToCacheAsync(string queryKey, List<ExternalRecipeSearchResult> results)
        {
            await EnsureSearchCacheReadyAsync();

            var existing = await _db.Table<SearchResultCacheEntry>().Where(c => c.QueryKey == queryKey).FirstOrDefaultAsync();
            string json = JsonSerializer.Serialize(results);

            if (existing != null)
            {
                existing.ResultsJson = json;
                existing.CachedAtUtc = DateTime.UtcNow;
                await _db.UpdateAsync(existing);
            }
            else
            {
                await _db.InsertAsync(new SearchResultCacheEntry { QueryKey = queryKey, ResultsJson = json, CachedAtUtc = DateTime.UtcNow });
            }
        }
    }
}