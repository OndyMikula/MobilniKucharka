#if ANDROID
using Android.Graphics;
#endif

namespace MobilniKucharka.Services
{
    public static class ImageResizeService
    {
        private const int MaxDimensionPx = 800;
        private const int JpegQuality = 85;

        public static async Task SaveResizedAsync(Stream sourceStream, string destinationPath)
        {
#if ANDROID
            // Načte celý stream do paměti nejdřív - MediaPicker.OpenReadAsync() stream nemusí
            // podporovat seek, takže spoléhat na Position = 0 u fallbacku (nedekódovatelný obrázek)
            // mohlo shodit appku výjimkou NotSupportedException. MemoryStream je vždy seekable.
            using var bufferedStream = new MemoryStream();
            await sourceStream.CopyToAsync(bufferedStream);
            bufferedStream.Position = 0;

            await Task.Run(() =>
            {
                var original = BitmapFactory.DecodeStream(bufferedStream);
                if (original == null)
                {
                    bufferedStream.Position = 0;
                    using var rawDest = File.Create(destinationPath);
                    bufferedStream.CopyTo(rawDest);
                    return;
                }

                int width = original.Width;
                int height = original.Height;
                double scale = Math.Min(1.0, (double)MaxDimensionPx / Math.Max(width, height));

                // "resized" je buď nová bitmapa (scale < 1.0), nebo přesně tentýž objekt jako
                // "original" (scale >= 1.0) - v tom druhém případě smí Dispose() proběhnout jen
                // JEDNOU, ne pro oba samostatně (dřívější bug - dvojité Dispose téhož objektu).
                Bitmap resized = scale < 1.0
                    ? Bitmap.CreateScaledBitmap(original, (int)(width * scale), (int)(height * scale), true)!
                    : original;

                try
                {
                    using var destStream = File.Create(destinationPath);
                    resized.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, destStream);
                }
                finally
                {
                    if (!ReferenceEquals(resized, original))
                    {
                        resized.Dispose();
                    }
                    original.Dispose();
                }
            });
#else
            using var destStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destStream);
#endif
        }
    }
}