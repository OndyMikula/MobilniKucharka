namespace MobilniKucharka.Services
{
    // Malý náhled pro RecipeCard v seznamu - oddělený od plného 800px obrázku detailu receptu
    // (ImageResizeService). Karta v seznamu nikdy nepotřebuje víc než pár desítek px výšky, takže
    // base64 kódování plného obrázku pro každou kartu zbytečně zpomaluje seznam při stovkách
    // receptů. Náhled se vygeneruje jednou a uloží do CacheDirectory (OS ho smí kdykoliv smazat,
    // znovu se dopočítá ze zdroje - klíč souboru obsahuje čas poslední změny zdroje).
    public static class ThumbnailService
    {
        private const int MaxDimensionPx = 160;
        private const int JpegQuality = 70;
        private const string ThumbnailSubfolder = "thumbnails";

        public static async Task<string> GetOrCreateThumbnailAsync(string sourceImagePath)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
                return sourceImagePath;

            string thumbDir = Path.Combine(FileSystem.CacheDirectory, ThumbnailSubfolder);
            Directory.CreateDirectory(thumbDir);

            var sourceInfo = new FileInfo(sourceImagePath);
            string cacheKey = $"{Path.GetFileNameWithoutExtension(sourceImagePath)}_{sourceInfo.LastWriteTimeUtc.Ticks}.jpg";
            string thumbPath = Path.Combine(thumbDir, cacheKey);

            if (File.Exists(thumbPath))
                return thumbPath;

            try
            {
                using var sourceStream = File.OpenRead(sourceImagePath);
                await ImageResizeService.SaveResizedAsync(sourceStream, thumbPath, MaxDimensionPx, JpegQuality);
                return thumbPath;
            }
            catch
            {
                return sourceImagePath;
            }
        }
    }
}