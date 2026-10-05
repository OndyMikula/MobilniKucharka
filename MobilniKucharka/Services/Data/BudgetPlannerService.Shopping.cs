using MobilniKucharka.Classes;
using MobilniKucharka.Classes.Shopping;

namespace MobilniKucharka.Services.Data
{
    public partial class BudgetPlannerService
    {
        private bool _isShoppingReady;

        private async Task EnsureShoppingReadyAsync()
        {
            await EnsureInitializedAsync();
            if (_isShoppingReady) return;

            await _db.CreateTableAsync<ShoppingList>();
            await _db.CreateTableAsync<ShoppingListItem>();
            _isShoppingReady = true;
        }

        public async Task<ShoppingList> CreateShoppingListAsync(string name)
        {
            await EnsureShoppingReadyAsync();

            var list = new ShoppingList { Name = name.Trim(), CreatedUtc = DateTime.UtcNow };
            await _db.InsertAsync(list);
            return list;
        }

        public async Task RenameShoppingListAsync(int listId, string name)
        {
            await EnsureShoppingReadyAsync();

            var list = await _db.Table<ShoppingList>().Where(l => l.Id == listId).FirstOrDefaultAsync();
            if (list == null) return;

            list.Name = name.Trim();
            await _db.UpdateAsync(list);
        }

        public async Task DeleteShoppingListAsync(int listId)
        {
            await EnsureShoppingReadyAsync();

            await _db.ExecuteAsync("DELETE FROM ShoppingListItem WHERE ListId = ?", listId);

            var list = await _db.Table<ShoppingList>().Where(l => l.Id == listId).FirstOrDefaultAsync();
            if (list != null) await _db.DeleteAsync(list);
        }

        public async Task<ShoppingList?> GetShoppingListAsync(int listId)
        {
            await EnsureShoppingReadyAsync();
            return await _db.Table<ShoppingList>().Where(l => l.Id == listId).FirstOrDefaultAsync();
        }

        public async Task<List<ShoppingList>> GetShoppingListsAsync()
        {
            await EnsureShoppingReadyAsync();
            var lists = await _db.Table<ShoppingList>().ToListAsync();
            return [.. lists.OrderBy(l => l.CreatedUtc)];
        }

        public async Task<List<ShoppingListSummary>> GetShoppingListSummariesAsync()
        {
            await EnsureShoppingReadyAsync();

            var lists = await GetShoppingListsAsync();
            var items = await _db.Table<ShoppingListItem>().ToListAsync();
            var products = (await GetProductsCachedAsync()).ToDictionary(p => p.Id);

            var result = new List<ShoppingListSummary>();
            foreach (var list in lists)
            {
                var entries = BuildShoppingEntries(items.Where(i => i.ListId == list.Id), products);
                result.Add(new ShoppingListSummary
                {
                    Id = list.Id,
                    Name = list.Name,
                    ItemCount = entries.Count,
                    TotalPrice = entries.Sum(e => e.Price),
                    HasUnpriced = entries.Any(e => e.IsUnpriced)
                });
            }
            return result;
        }

        public async Task<List<ShoppingListEntry>> GetShoppingListEntriesAsync(int listId)
        {
            await EnsureShoppingReadyAsync();

            var items = await _db.Table<ShoppingListItem>().Where(i => i.ListId == listId).ToListAsync();
            var products = (await GetProductsCachedAsync()).ToDictionary(p => p.Id);
            return BuildShoppingEntries(items, products);
        }

        public async Task<HashSet<int>> GetShoppingListIdsForRecipeAsync(int recipeId)
        {
            await EnsureShoppingReadyAsync();

            var items = await _db.Table<ShoppingListItem>().Where(i => i.RecipeId == recipeId).ToListAsync();
            return [.. items.Select(i => i.ListId)];
        }

        // Přidá suroviny receptu do seznamu, opakované přidání nahradí staré položky
        public async Task AddRecipeToShoppingListAsync(int listId, int recipeId, IEnumerable<DisplayIngredient> ingredients)
        {
            await EnsureShoppingReadyAsync();

            await _db.ExecuteAsync("DELETE FROM ShoppingListItem WHERE ListId = ? AND RecipeId = ?", listId, recipeId);

            var products = (await GetProductsCachedAsync()).ToDictionary(p => p.Id);

            foreach (var ing in ingredients)
            {
                products.TryGetValue(ing.ProductId, out var product);
                bool hasAmount = ing.RawAmount > 0 && product != null;

                await _db.InsertAsync(new ShoppingListItem
                {
                    ListId = listId,
                    RecipeId = recipeId,
                    ProductId = ing.ProductId,
                    Name = ing.Name,
                    Amount = hasAmount ? ing.RawAmount : 0,
                    Unit = hasAmount ? product!.Unit : string.Empty,
                    AmountText = hasAmount ? string.Empty : ing.AmountText
                });
            }
        }

        public async Task RemoveRecipeFromShoppingListAsync(int listId, int recipeId)
        {
            await EnsureShoppingReadyAsync();
            await _db.ExecuteAsync("DELETE FROM ShoppingListItem WHERE ListId = ? AND RecipeId = ?", listId, recipeId);
        }

        // Spojí stejnou surovinu z více receptů do jednoho řádku
        private static List<ShoppingListEntry> BuildShoppingEntries(IEnumerable<ShoppingListItem> items, Dictionary<int, LocalProduct> products)
        {
            string lang = Preferences.Default.Get("AppLanguageCode", "cs");
            var entries = new List<ShoppingListEntry>();

            foreach (var group in items.GroupBy(i => (i.ProductId, i.Unit)))
            {
                var first = group.First();
                products.TryGetValue(first.ProductId, out var product);

                string name = product == null ? first.Name : (lang == "cs" ? product.Name_CS : product.Name_EN);
                if (string.IsNullOrWhiteSpace(name)) name = first.Name;

                double amount = group.Sum(i => i.Amount);

                string amountText = amount > 0
                    ? $"{amount:0.#} {first.Unit}"
                    : string.Join(", ", group.Select(i => i.AmountText).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct());

                double price = amount > 0 && product != null ? Math.Round(amount * product.EffectivePrice, 0) : 0;

                entries.Add(new ShoppingListEntry
                {
                    Name = name,
                    AmountText = amountText,
                    Price = price,
                    IsUnpriced = amount > 0 && price <= 0
                });
            }

            return [.. entries.OrderBy(e => e.Name)];
        }
    }
}