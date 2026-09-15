using System.Globalization;
using System.Text.Json;

namespace MobilniKucharka.Services.Api
{
    //Open Food Facts API v aplikaci zobrazujenutricni hodnoty, vyuziva moznosti nacteni carovych kodu ze primo v aplikaci
    //text Je toto ten sýr ? Button Načíst čárový kód ktery kdyz to najde v databazi tak rekne Jo to je presne on
    //kdyz to nenajde tak to da tu defaultni hlasku + Chceš přidat potravinu do databaze ? Ano/Ne
    //pri ano tak aplikace chce vyplnit potrebne udaje co potrebuje to API aby to mohla poslat dal do databaze pro vyvojare API
    //pak po vyplneni vsech polí button Poslat posle data vyvojarum API
    public static class OpenFoodFactsService
    {
        // Static klient = jeden hlavičkový setup navždy, ne per-instance (dřív by druhá "instance"
        // duplikovala User-Agent hlavičku na sdíleném klientovi).
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Add("User-Agent", "MobilniKucharka - Android - Version 1.0 - VypisNutricnichHodnot (Kontakt: zoufalyondrej@gmail.com)");
            return client;
        }

        // 1. ZÍSKÁNÍ DAT PODLE ČÁROVÉHO KÓDU
        public static async Task<OffProduct?> GetProductByBarcodeAsync(string barcode)
        {
            string url = $"https://world.openfoodfacts.org/api/v3/product/{barcode}.json";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var contentString = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(contentString) || (!contentString.StartsWith('{') && !contentString.StartsWith('[')))
                {
                    return null;
                }

                var root = JsonSerializer.Deserialize<JsonElement>(contentString);

                if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "success")
                {
                    if (root.TryGetProperty("product", out var productData))
                    {
                        string name = "Neznámý produkt";
                        if (productData.TryGetProperty("product_name_cs", out var nameCs) && !string.IsNullOrEmpty(nameCs.GetString()))
                        {
                            name = nameCs.GetString()!;
                        }
                        else if (productData.TryGetProperty("product_name", out var nameEn) && !string.IsNullOrEmpty(nameEn.GetString()))
                        {
                            name = nameEn.GetString()!;
                        }

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
                return null;
            }
            catch
            {
                return null;
            }
        }

        // 2. ODESLÁNÍ NOVÉHO PRODUKTU (Příspěvek vývojářům API)
        public static async Task<bool> UploadNewProductAsync(string barcode, string name, double protein, double carbs, double fat)
        {
            string url = "https://world.openfoodfacts.org/cgi/product_jqm2.pl";

            var fields = new Dictionary<string, string>
            {
                { "code", barcode },
                { "product_name", name },
                { "nutriment_proteins_100g", protein.ToString(CultureInfo.InvariantCulture) },
                { "nutriment_carbohydrates_100g", carbs.ToString(CultureInfo.InvariantCulture) },
                { "nutriment_fat_100g", fat.ToString(CultureInfo.InvariantCulture) },
                { "user_id", Secrets.OpenFoodFactsUserId },
                { "password", Secrets.OpenFoodFactsPassword }
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