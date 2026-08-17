using System.IO.Compression;
using System.Text.Json;

namespace MobilniKucharka.Classes.Recipe.Sharing
{
    public class SharedRecipeData
    {
        // Značka a verze formátu - appka podle nich hned pozná, jestli soubor vůbec je (nebo NENÍ)
        // recept Mobilní Kuchařky, než se ho pokusí zpracovat. Bez týhle kontroly by appka zkoušela
        // naimportovat úplně libovolný JSON soubor.
        public string FormatMarker { get; set; } = "MobilniKucharkaRecipe";
        public int FormatVersion { get; set; } = 1;

        public string Name_CS { get; set; } = "";
        public string Name_EN { get; set; } = "";
        public string DescriptionText { get; set; } = "";
        public string IngredientsRaw { get; set; } = "";
        public string StepsJson_CS { get; set; } = "";
        public string StepsJson_EN { get; set; } = "";
        public string EquipmentJson { get; set; } = "";
        public string DietaryFlagsJson { get; set; } = "";
        public double Rating { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Sugar { get; set; }
        public bool IsNutritionEstimated { get; set; }
        public double ManualCost { get; set; }
        public int PrepTime { get; set; }
        public int ServingSize { get; set; }
        public bool HasPhoto { get; set; }
        public string? PhotoBase64 { get; set; }
    }

    public class RecipeShareService
    {
        private const int MaxNameLength = 200;
        private const int MaxTextFieldLength = 5000;   // popis receptu
        private const int MaxIngredientsLength = 20000; // celý seznam surovin najednou
        private const int MaxStepsJsonLength = 20000;
        private const long MaxPhotoBytes = 8 * 1024 * 1024; // 8 MB

        public static async Task<string> ExportRecipeAsync(Recipe recipe)
        {
            string exportPath = Path.Combine(FileSystem.CacheDirectory, $"recept_{SanitizeFileName(recipe.Name_CS)}.mkrecept");
            if (File.Exists(exportPath)) File.Delete(exportPath);

            bool hasPhoto = !string.IsNullOrWhiteSpace(recipe.ImageUrl) && File.Exists(recipe.ImageUrl);

            var shared = new SharedRecipeData
            {
                Name_CS = recipe.Name_CS,
                Name_EN = recipe.Name_EN,
                DescriptionText = recipe.DescriptionText,
                IngredientsRaw = recipe.IngredientsRaw,
                StepsJson_CS = recipe.StepsJson_CS,
                StepsJson_EN = recipe.StepsJson_EN,
                EquipmentJson = recipe.EquipmentJson,
                DietaryFlagsJson = recipe.DietaryFlagsJson,
                Rating = recipe.Rating,
                Protein = recipe.Protein,
                Carbs = recipe.Carbs,
                Fat = recipe.Fat,
                Sugar = recipe.Sugar,
                IsNutritionEstimated = recipe.IsNutritionEstimated,
                ManualCost = recipe.ManualCost,
                PrepTime = recipe.PrepTime,
                ServingSize = recipe.ServingSize,
                HasPhoto = hasPhoto
            };

            await Task.Run(() =>
            {
                using var zip = ZipFile.Open(exportPath, ZipArchiveMode.Create);

                var jsonEntry = zip.CreateEntry("recipe.json");
                using (var writer = new StreamWriter(jsonEntry.Open()))
                {
                    writer.Write(JsonSerializer.Serialize(shared));
                }

                if (hasPhoto)
                {
                    zip.CreateEntryFromFile(recipe.ImageUrl, "photo.jpg");
                }
            });

            return exportPath;
        }

        public static async Task<Recipe> ImportRecipeAsync(string filePath)
        {
            SharedRecipeData? shared = null;
            string? photoDestPath = null;

            await Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(filePath);

                var jsonEntry = zip.GetEntry("recipe.json") ?? throw new InvalidOperationException("Soubor neobsahuje platný recept.");
                using (var reader = new StreamReader(jsonEntry.Open()))
                {
                    shared = JsonSerializer.Deserialize<SharedRecipeData>(reader.ReadToEnd());
                }

                var photoEntry = zip.GetEntry("photo.jpg");
                if (photoEntry != null)
                {
                    photoDestPath = Path.Combine(FileSystem.AppDataDirectory, $"{Guid.NewGuid()}_sdileny_recept.jpg");
                    photoEntry.ExtractToFile(photoDestPath, overwrite: true);
                }
            });

            if (shared == null) throw new InvalidOperationException("Recept se nepodařilo přečíst.");

            ValidateSharedRecipe(shared);

            return new Recipe
            {
                Name_CS = shared.Name_CS,
                Name_EN = shared.Name_EN,
                DescriptionText = shared.DescriptionText,
                IngredientsRaw = shared.IngredientsRaw,
                StepsJson_CS = SanitizeNestedJson(shared.StepsJson_CS),
                StepsJson_EN = SanitizeNestedJson(shared.StepsJson_EN),
                EquipmentJson = SanitizeNestedJson(shared.EquipmentJson),
                DietaryFlagsJson = SanitizeNestedJson(shared.DietaryFlagsJson),
                Protein = shared.Protein,
                Carbs = shared.Carbs,
                Fat = shared.Fat,
                Sugar = shared.Sugar,
                IsNutritionEstimated = shared.IsNutritionEstimated,
                ManualCost = shared.ManualCost,
                PrepTime = shared.PrepTime,
                ServingSize = shared.ServingSize,
                Category = "Vytvořené recepty",
                ImageUrl = photoDestPath ?? ""
            };
        }

        // Nová cesta: recept jako obyčejný .json soubor (žádný .zip), aby ho šlo napsat i ručně mimo appku.
        public static async Task<string> ExportRecipeAsJsonAsync(Recipe recipe)
        {
            bool hasPhoto = !string.IsNullOrWhiteSpace(recipe.ImageUrl) && File.Exists(recipe.ImageUrl);

            var shared = new SharedRecipeData
            {
                Name_CS = recipe.Name_CS,
                Name_EN = recipe.Name_EN,
                DescriptionText = recipe.DescriptionText,
                IngredientsRaw = recipe.IngredientsRaw,
                StepsJson_CS = recipe.StepsJson_CS,
                StepsJson_EN = recipe.StepsJson_EN,
                EquipmentJson = recipe.EquipmentJson,
                DietaryFlagsJson = recipe.DietaryFlagsJson,
                Rating = recipe.Rating,
                Protein = recipe.Protein,
                Carbs = recipe.Carbs,
                Fat = recipe.Fat,
                Sugar = recipe.Sugar,
                IsNutritionEstimated = recipe.IsNutritionEstimated,
                ManualCost = recipe.ManualCost,
                PrepTime = recipe.PrepTime,
                ServingSize = recipe.ServingSize,
                HasPhoto = hasPhoto,
                PhotoBase64 = hasPhoto ? Convert.ToBase64String(await File.ReadAllBytesAsync(recipe.ImageUrl)) : null
            };

            string exportPath = Path.Combine(FileSystem.CacheDirectory, $"recept_{SanitizeFileName(recipe.Name_CS)}.json");
            if (File.Exists(exportPath)) File.Delete(exportPath);

            string json = JsonSerializer.Serialize(shared, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(exportPath, json);

            return exportPath;
        }

        public static async Task<Recipe> ImportRecipeFromJsonAsync(string filePath)
        {
            string json = await File.ReadAllTextAsync(filePath);

            SharedRecipeData? shared;
            try
            {
                shared = JsonSerializer.Deserialize<SharedRecipeData>(json);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("Tento soubor nejde přečíst jako recept (poškozený nebo neplatný JSON).");
            }

            if (shared == null) throw new InvalidOperationException("Recept se nepodařilo přečíst.");

            ValidateSharedRecipe(shared);

            string? photoDestPath = null;
            if (!string.IsNullOrWhiteSpace(shared.PhotoBase64))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(shared.PhotoBase64);
                    if (bytes.LongLength <= MaxPhotoBytes)
                    {
                        photoDestPath = Path.Combine(FileSystem.AppDataDirectory, $"{Guid.NewGuid()}_json_recept.jpg");
                        await File.WriteAllBytesAsync(photoDestPath, bytes);
                    }
                    // Přes limit -> recept se naimportuje bez obrázku, ne s chybou.
                }
                catch (FormatException)
                {
                    // Poškozený base64 - recept naimportujeme i tak, jen bez obrázku.
                }
            }

            return new Recipe
            {
                Name_CS = shared.Name_CS,
                Name_EN = shared.Name_EN,
                DescriptionText = shared.DescriptionText,
                IngredientsRaw = shared.IngredientsRaw,
                StepsJson_CS = SanitizeNestedJson(shared.StepsJson_CS),
                StepsJson_EN = SanitizeNestedJson(shared.StepsJson_EN),
                EquipmentJson = SanitizeNestedJson(shared.EquipmentJson),
                DietaryFlagsJson = SanitizeNestedJson(shared.DietaryFlagsJson),
                Protein = shared.Protein,
                Carbs = shared.Carbs,
                Fat = shared.Fat,
                Sugar = shared.Sugar,
                IsNutritionEstimated = shared.IsNutritionEstimated,
                ManualCost = shared.ManualCost,
                PrepTime = shared.PrepTime,
                ServingSize = shared.ServingSize,
                Category = "Vytvořené recepty",
                ImageUrl = photoDestPath ?? ""
            };
        }

        // Ověří značku formátu a rozumné meze na velikost polí, než se recept vůbec dostane do DB.
        // Vyhazuje InvalidOperationException se srozumitelnou zprávou pro DisplayAlert na volající straně.
        private static void ValidateSharedRecipe(SharedRecipeData shared)
        {
            if (shared.FormatMarker != "MobilniKucharkaRecipe")
                throw new InvalidOperationException("Tento soubor není recept ve formátu Mobilní Kuchařky.");

            if (shared.FormatVersion > 1)
                throw new InvalidOperationException("Tento recept byl vytvořen novější verzí aplikace, kterou tahle verze zatím neumí přečíst.");

            if (string.IsNullOrWhiteSpace(shared.Name_CS) && string.IsNullOrWhiteSpace(shared.Name_EN))
                throw new InvalidOperationException("Recept nemá vyplněný název.");

            if ((shared.Name_CS?.Length ?? 0) > MaxNameLength || (shared.Name_EN?.Length ?? 0) > MaxNameLength)
                throw new InvalidOperationException("Název receptu je neobvykle dlouhý.");

            if ((shared.DescriptionText?.Length ?? 0) > MaxTextFieldLength)
                throw new InvalidOperationException("Popis receptu je neobvykle dlouhý.");

            if ((shared.IngredientsRaw?.Length ?? 0) > MaxIngredientsLength)
                throw new InvalidOperationException("Seznam surovin je neobvykle dlouhý.");

            if ((shared.StepsJson_CS?.Length ?? 0) > MaxStepsJsonLength || (shared.StepsJson_EN?.Length ?? 0) > MaxStepsJsonLength)
                throw new InvalidOperationException("Postup přípravy je neobvykle dlouhý.");

            if (shared.ServingSize < 0 || shared.ServingSize > 100)
                throw new InvalidOperationException("Počet porcí není platná hodnota.");

            if (shared.PrepTime < 0 || shared.PrepTime > 1440)
                throw new InvalidOperationException("Doba přípravy není platná hodnota.");

            if (shared.ManualCost < 0 || shared.ManualCost > 1_000_000)
                throw new InvalidOperationException("Cena receptu není platná hodnota.");
        }

        // Ověří, že vnořený JSON (kroky/pomůcky/diety) je platné pole řetězců, než se uloží do DB -
        // jinak by appka spadla později, kdykoliv by se tenhle recept zobrazil (viz Recipe.cs Steps_CS/EN gettery).
        private static string SanitizeNestedJson(string? rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return "[]";
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(rawJson);
                return list != null ? JsonSerializer.Serialize(list) : "[]";
            }
            catch (JsonException)
            {
                return "[]";
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 40 ? name[..40] : name;
        }
    }
}