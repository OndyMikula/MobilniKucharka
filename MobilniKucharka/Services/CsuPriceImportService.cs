namespace MobilniKucharka.Services
{
    public class CsuPriceEntry
    {
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double Price { get; set; }
        public string Month { get; set; } = string.Empty; // "2026-06"
    }

    public class CsuPriceImportService
    {
        private readonly HttpClient _httpClient = new();
        private const string CsvUrl = "https://data.csu.gov.cz/opendata/sady/CEN02/distribuce/csv";

        public async Task<List<CsuPriceEntry>> FetchLatestConsumerPricesAsync()
        {
            var latest = new Dictionary<string, CsuPriceEntry>(); // klíč = název produktu bez prefixu/jednotky

            try
            {
                string csv = await _httpClient.GetStringAsync(CsvUrl);
                using var reader = new StringReader(csv);

                string? line = reader.ReadLine(); // hlavička, přeskočíme
                while ((line = reader.ReadLine()) != null)
                {
                    var fields = ParseCsvLine(line);
                    if (fields.Length < 7) continue;

                    string indicator = fields[0]; // např. "S  Hovězí maso zadní bez kosti [kg]"
                    string month = fields[5];     // "2026-06"
                    string valueRaw = fields[6];

                    if (!indicator.StartsWith("S ")) continue; // chceme jen spotřebitelské ceny
                    if (!double.TryParse(valueRaw, System.Globalization.CultureInfo.InvariantCulture, out double price)) continue;

                    var (name, unit) = SplitNameAndUnit(indicator);
                    string key = name; // "- od 2026" už je odstraněno v SplitNameAndUnit

                    if (!latest.TryGetValue(key, out var existing) || string.CompareOrdinal(month, existing.Month) > 0)
                    {
                        latest[key] = new CsuPriceEntry { ProductName = name, Unit = unit, Price = price, Month = month };
                    }
                }
            }
            catch
            {
                return []; // bez internetu / nedostupné -> prázdný seznam, volající strana ponechá stávající ceny
            }

            return [.. latest.Values];
        }

        private static (string Name, string Unit) SplitNameAndUnit(string indicator)
        {
            // Odstraníme prefix "S  " a případný suffix " - od 2026"
            string cleaned = indicator[1..].Trim();
            cleaned = cleaned.Replace(" - od 2026", "").Trim();

            int bracketStart = cleaned.LastIndexOf('[');
            int bracketEnd = cleaned.LastIndexOf(']');

            if (bracketStart > 0 && bracketEnd > bracketStart)
            {
                string name = cleaned[..bracketStart].Trim();
                string unit = cleaned[(bracketStart + 1)..bracketEnd].Trim();
                return (name, unit);
            }

            return (cleaned, "");
        }

        private static string[] ParseCsvLine(string line)
        {
            // Jednoduchý CSV parser respektující uvozovky (hodnoty jsou obalené v " ")
            return [.. line.Split(',').Select(f => f.Trim('"'))];
        }
    }
}