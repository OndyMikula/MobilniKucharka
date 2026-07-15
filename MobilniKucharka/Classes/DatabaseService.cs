using SQLite;
using MobilniKucharka.Classes;

namespace MobilniKucharka
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;

        private async Task InitAsync()
        {
            if (_database != null)
                return;

            // Definice cesty do zabezpečeného interního úložiště aplikace
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "kucharka.db3");
            _database = new SQLiteAsyncConnection(dbPath);

            // Vytvoření tabulek (pokud již existují, metoda neudělá nic)
            await _database.CreateTableAsync<Recipe>();
            await _database.CreateTableAsync<LocalProduct>();
        }

        // --- MANIPULACE S PRODUKTY ---

        public async Task SaveProductAsync(LocalProduct product)
        {
            await InitAsync();
            await _database!.InsertOrReplaceAsync(product);
        }

        public async Task<LocalProduct?> GetProductByIdAsync(string id)
        {
            await InitAsync();
            return await _database!.Table<LocalProduct>().Where(p => p.Id == id).FirstOrDefaultAsync();
        }

        // --- ROBUSTNÍ CSV IMPORT ---

        public async Task ImportCsvDatabaseAsync(Stream csvStream)
        {
            await InitAsync();

            using var reader = new StreamReader(csvStream);
            var headerLine = await reader.ReadLineAsync(); // Přeskočení hlavičky

            var productsToInsert = new List<LocalProduct>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(';');
                if (parts.Length < 6) continue;

                try
                {
                    productsToInsert.Add(new LocalProduct
                    {
                        Id = parts[0].Trim(),
                        Name = parts[1].Trim(),
                        Protein = ParseDoubleSafe(parts[2]),
                        Carbs = ParseDoubleSafe(parts[3]),
                        Fat = ParseDoubleSafe(parts[4]),
                        Sugar = ParseDoubleSafe(parts[5]),
                        IsFromCsv = true
                    });
                }
                catch
                {
                    // Odolnost proti poškozeným řádkům – pokračujeme dál
                    continue;
                }
            }

            if (productsToInsert.Any())
            {
                // Zápis stovek položek je nutné obalit do transakce pro maximální výkon
                await _database!.RunInTransactionAsync(tran =>
                {
                    foreach (var prod in productsToInsert)
                    {
                        tran.InsertOrReplace(prod);
                    }
                });
            }
        }

        private double ParseDoubleSafe(string value)
        {
            value = value.Replace(',', '.').Trim();
            return double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
        }
    }
}