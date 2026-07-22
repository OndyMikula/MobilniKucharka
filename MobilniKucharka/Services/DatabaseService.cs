using MobilniKucharka.Classes;
using MobilniKucharka.Classes.Recipe;
using SQLite;

namespace MobilniKucharka.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _database;

    // Změněno na public, abychom to mohli zavolat při startu aplikace
    public async Task InitAsync()
    {
        if (_database != null)
            return;

        // Definice cesty do zabezpečeného interního úložiště aplikace
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "kucharka.db3");
        _database = new SQLiteAsyncConnection(dbPath);

        // Vytvoření všech tří tabulek (pokud již existují, metoda neudělá nic)
        await _database.CreateTableAsync<Recipe>();
        await _database.CreateTableAsync<LocalProduct>();
        await _database.CreateTableAsync<RecipeIngredient>();
    }

    // --- MANIPULACE S PRODUKTY ---
    public async Task SaveProductAsync(LocalProduct product)
    {
        await InitAsync();
        await _database!.InsertOrReplaceAsync(product);
    }

    public async Task<LocalProduct?> GetProductByIdAsync(int id)
    {
        await InitAsync();
        return await _database!.Table<LocalProduct>().Where(p => p.Id == id).FirstOrDefaultAsync();
    }
}