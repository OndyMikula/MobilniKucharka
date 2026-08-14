using System.IO.Compression;
using System.Text.Json;

namespace MobilniKucharka.Classes.Recipe.Sharing
{
    public class SharedRecipeData
    {
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
    }

    public class RecipeShareService
    {
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

            return new Recipe
            {
                Name_CS = shared.Name_CS,
                Name_EN = shared.Name_EN,
                DescriptionText = shared.DescriptionText,
                IngredientsRaw = shared.IngredientsRaw,
                StepsJson_CS = shared.StepsJson_CS,
                StepsJson_EN = shared.StepsJson_EN,
                EquipmentJson = shared.EquipmentJson,
                DietaryFlagsJson = shared.DietaryFlagsJson,
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

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length > 40 ? name[..40] : name;
        }
    }
}
