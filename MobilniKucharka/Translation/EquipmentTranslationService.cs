using MobilniKucharka.Classes;
using System.Text.Json;

namespace MobilniKucharka.Translation
{
    // Lokální slovník překladů kuchyňských pomůcek (Pánev <-> Pan apod.), uložený jako JSON v
    // AppDataDirectory - drží nejvýše MaxEntries nejpoužívanějších párů, sdílené napříč VŠEMI
    // recepty (na rozdíl od RecipeTranslationCache, vázané na jeden recept).
    public static class EquipmentTranslationService
    {
        private const string FileName = "equipment_translations.json";
        private const int MaxEntries = 30;

        private static List<EquipmentTranslationEntry>? _cache;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        private static string FilePath => Path.Combine(FileSystem.AppDataDirectory, FileName);

        private static async Task<List<EquipmentTranslationEntry>> LoadAsync()
        {
            if (_cache != null) return _cache;

            await _lock.WaitAsync();
            try
            {
                if (_cache != null) return _cache;

                if (File.Exists(FilePath))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(FilePath);
                        _cache = JsonSerializer.Deserialize<List<EquipmentTranslationEntry>>(json) ?? [];
                    }
                    catch (JsonException)
                    {
                        _cache = [];
                    }
                }
                else
                {
                    _cache = [];
                }

                return _cache;
            }
            finally
            {
                _lock.Release();
            }
        }

        private static async Task SaveAsync(List<EquipmentTranslationEntry> entries) =>
            await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(entries));

        private static string Capitalize(string text)
        {
            text = text.Trim();
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
        }

        // Vrátí přeložený název pomůcky. Neznámý pár zavolá DeepL jednou a uloží ho (case-insensitive
        // porovnání, nový záznam s velkým prvním písmenem). Vrátí původní text, pokud DeepL selže.
        public static async Task<string> TranslateEquipmentNameAsync(string name, string fromLang, string toLang)
        {
            string trimmed = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return name;

            var entries = await LoadAsync();

            var existing = entries.FirstOrDefault(e =>
                string.Equals(fromLang == "cs" ? e.Name_CS : e.Name_EN, trimmed, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.UsageCount++;
                await SaveAsync(entries);
                return fromLang == "cs" ? existing.Name_EN : existing.Name_CS;
            }

            string? translated = await TranslationService.TranslateAsync(trimmed, toLang, fromLang);
            if (string.IsNullOrWhiteSpace(translated)) return name;

            entries.Add(new EquipmentTranslationEntry
            {
                Name_CS = Capitalize(fromLang == "cs" ? trimmed : translated),
                Name_EN = Capitalize(fromLang == "cs" ? translated : trimmed),
                UsageCount = 1
            });

            if (entries.Count > MaxEntries)
                entries = [.. entries.OrderByDescending(e => e.UsageCount).Take(MaxEntries)];

            await SaveAsync(entries);
            return translated;
        }

        public static async Task<List<string>> TranslateEquipmentListAsync(List<string> names, string fromLang, string toLang)
        {
            var result = new List<string>();
            foreach (var name in names)
                result.Add(await TranslateEquipmentNameAsync(name, fromLang, toLang));
            return result;
        }
    }
}