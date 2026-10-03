using System.Globalization;
using System.Text;
using MobilniKucharka.Classes;

namespace MobilniKucharka.Services
{
    // Lehký, cílený export jednotlivých částí dat (na rozdíl od DataBackupService, který zabalí
    // úplně vše) - určeno pro ladění/sdílení konkrétního souboru s vývojářem bez nutnosti
    // rozbalovat celou zálohu.
    public static class DeveloperExportService
    {
        public static async Task<string> ExportIngredientsCsvAsync(List<LocalProduct> products)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Name_CS,Name_EN,Unit,PriceAverage,HasManualPrice,ManualPrice,TypicalUnitWeightGrams");

            foreach (var p in products)
            {
                sb.AppendLine(string.Join(",",
                    p.Id,
                    CsvEscape(p.Name_CS),
                    CsvEscape(p.Name_EN),
                    CsvEscape(p.Unit),
                    p.PriceAverage.ToString(CultureInfo.InvariantCulture),
                    p.HasManualPrice,
                    p.ManualPrice.ToString(CultureInfo.InvariantCulture),
                    p.TypicalUnitWeightGrams.ToString(CultureInfo.InvariantCulture)));
            }

            string path = Path.Combine(FileSystem.CacheDirectory, "suroviny_export.csv");
            await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        public static async Task<string> ExportSettingsJsonAsync()
        {
            string json = PreferencesBackupService.CaptureAsJson();
            string path = Path.Combine(FileSystem.CacheDirectory, "nastaveni_export.json");
            await File.WriteAllTextAsync(path, json, Encoding.UTF8);
            return path;
        }

        private static string CsvEscape(string? value)
        {
            value ??= "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}