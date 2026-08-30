#if ANDROID
using Android.Graphics;
#endif

namespace MobilniKucharka.Services
{
    // Zmenší a znovu zakóduje obrázek před uložením na disk - používá se všude, kde uživatel vybírá
    // fotku z galerie a appka ji pak zobrazuje jen jako malý náhled (záložky ~150-180px karty,
    // recepty ~120px miniatura v RecipeCard.razor). Bez tohohle appka ukládala fotky ve full
    // rozlišení přímo z fotoaparátu (běžně několik MB) - ImageHelper.ResolveImageSrc je pak
    // base64-zakóduje pro zobrazení v Blazor <img>, a příliš velké výsledné data: URI Android
    // WebView občas vůbec nevykreslí, potichu, bez chyby (přesně příznak "obrázek je vidět v
    // nativní editaci/náhledu, ale na kartě v Blazoru svítí jen výchozí barva pozadí" - nativní
    // MAUI Image zvládne libovolně velký soubor přímo ze souborového systému, kdežto Blazor <img>
    // musí projít base64 data: URI, kde reálný limit existuje).
    public static class ImageResizeService
    {
        private const int MaxDimensionPx = 800;
        private const int JpegQuality = 85;

        public static async Task SaveResizedAsync(Stream sourceStream, string destinationPath)
        {
#if ANDROID
            await Task.Run(() =>
            {
                using var original = BitmapFactory.DecodeStream(sourceStream);
                if (original == null)
                {
                    // Nepodařilo se dekódovat jako bitmapu (neobvyklý/poškozený formát) - uložíme
                    // raději originál beze změny, než abychom o fotku úplně přišli.
                    sourceStream.Position = 0;
                    using var rawDest = File.Create(destinationPath);
                    sourceStream.CopyTo(rawDest);
                    return;
                }

                int width = original.Width;
                int height = original.Height;
                double scale = Math.Min(1.0, (double)MaxDimensionPx / Math.Max(width, height));

                using Bitmap resized = scale < 1.0
                    ? Bitmap.CreateScaledBitmap(original, (int)(width * scale), (int)(height * scale), true)!
                    : original;

                using var destStream = File.Create(destinationPath);
                resized.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, destStream);
            });
#else
            // Fallback pro případný budoucí multi-target build bez Androidu - appka aktuálně cílí
            // jen net10.0-android (viz CLAUDE.md), tahle větev se v praxi nevolá.
            using var destStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destStream);
#endif
        }
    }
}