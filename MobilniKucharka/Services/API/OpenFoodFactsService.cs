using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace MobilniKucharka.Services.Api
{
    //Open Food Facts API v aplikaci zobrazujenutricni hodnoty, vyuziva moznosti nacteni carovych kodu ze primo v aplikaci
    //text Je toto ten sýr ? Button Načíst čárový kód ktery kdyz to najde v databazi tak rekne Jo to je presne on
    //kdyz to nenajde tak to da tu defaultni hlasku + Chceš přidat potravinu do databaze ? Ano/Ne
    //pri ano tak aplikace chce vyplnit potrebne udaje co potrebuje to API aby to mohla poslat dal do databaze pro vyvojare API
    //pak po vyplneni vsech polí button Poslat posle data vyvojarum API
    public class OpenFoodFactsService
    {
        private readonly HttpClient _httpClient = new();

        public OpenFoodFactsService()
        {
            // OpenFoodFacts vyžaduje nastavení User-Agenta s názvem tvé aplikace
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MobilniKucharka - Android - Version 1.0 - VypisNutricnichHodnot (Kontakt: zoufalyondrej@gmail.com)");
        }

        // 1. ZÍSKÁNÍ DAT PODLE ČÁROVÉHO KÓDU
        public async Task<OffProduct?> GetProductByBarcodeAsync(string barcode)
        {
            string url = $"https://world.openfoodfacts.org/api/v3/product/{barcode}.json";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                // 1. Načteme to jen jako hloupý text
                var contentString = await response.Content.ReadAsStringAsync();

                // 2. Zkontrolujeme, jestli to vůbec vypadá jako JSON (JSON vždy začíná { nebo [ )
                if (string.IsNullOrWhiteSpace(contentString) || (!contentString.StartsWith('{')  && !contentString.StartsWith('[')))
                {
                    return null; // Zabalíme to dřív, než to stihne spadnout
                }

                // 3. Až teď to bezpečně převedeme
                var root = JsonSerializer.Deserialize<JsonElement>(contentString);

                // Bezpečné ověření statusu
                if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "success")
                {
                    if (root.TryGetProperty("product", out var productData))
                    {
                        // Bezpečné získání názvu (nejprve čeština, pak obecný název, případně fallback)
                        string name = "Neznámý produkt";
                        if (productData.TryGetProperty("product_name_cs", out var nameCs) && !string.IsNullOrEmpty(nameCs.GetString()))
                        {
                            name = nameCs.GetString()!;
                        }
                        else if (productData.TryGetProperty("product_name", out var nameEn) && !string.IsNullOrEmpty(nameEn.GetString()))
                        {
                            name = nameEn.GetString()!;
                        }

                        // Bezpečné načtení nutričních hodnot
                        double kcal = 0, protein = 0, carbs = 0, fat = 0;
                        if (productData.TryGetProperty("nutriments", out var nutrients))
                        {
                            kcal = GetNutrientValue(nutrients, "energy-kcal_100g");
                            protein = GetNutrientValue(nutrients, "proteins_100g");
                            carbs = GetNutrientValue(nutrients, "carbohydrates_100g");
                            fat = GetNutrientValue(nutrients, "fat_100g");
                        }

                        return new OffProduct
                        {
                            Barcode = barcode,
                            Name = name,
                            Kcal = kcal,
                            Protein = protein,
                            Carbs = carbs,
                            Fat = fat
                        };
                    }
                }
                return null; // Produkt nenalezen
            }
            catch
            {
                return null;
            }
        }

        // 2. ODESLÁNÍ NOVÉHO PRODUKTU (Příspěvek vývojářům API)
        public async Task<bool> UploadNewProductAsync(string barcode, string name, double protein, double carbs, double fat)
        {
            string url = "https://world.openfoodfacts.org/cgi/product_jqm2.pl";

            // OPRAVENO: Použití CultureInfo.InvariantCulture zajistí, že se double převede s TEČKOU (např. 12.5 místo 12,5)
            var fields = new Dictionary<string, string>
            {
                { "code", barcode },
                { "product_name", name },
                { "nutriment_proteins_100g", protein.ToString(CultureInfo.InvariantCulture) },
                { "nutriment_carbohydrates_100g", carbs.ToString(CultureInfo.InvariantCulture) },
                { "nutriment_fat_100g", fat.ToString(CultureInfo.InvariantCulture) },
                { "user_id", "OndyMikula_App" },
                { "password", "OpenFoodFactsPassword" }
            };

            var content = new FormUrlEncodedContent(fields);
            try
            {
                var response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // OPRAVENO: Robustnější parsování hodnot, které ošetřuje i situaci, kdy API vrátí číslo jako text (string)
        private static double GetNutrientValue(JsonElement nutrients, string key)
        {
            if (nutrients.TryGetProperty(key, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number)
                {
                    return val.GetDouble();
                }
                if (val.ValueKind == JsonValueKind.String)
                {
                    if (double.TryParse(val.GetString(), CultureInfo.InvariantCulture, out double parsedVal))
                    {
                        return parsedVal;
                    }
                }
            }
            return 0;
        }
    }

    public class OffProduct
    {
        public string? Barcode { get; set; }
        public string? Name { get; set; }
        public double Kcal { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
    }
}