using System.Diagnostics;

namespace MobilniKucharka.Services
{
    public static class ImageHelper
    {
        private static readonly Dictionary<string, string> Base64Cache = new();
        private const int MaxCacheEntries = 50;

        public static string ResolveImageSrc(string? pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
                return string.Empty;

            // If already a web URL or data URI, return directly
            if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                pathOrUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return pathOrUrl;
            }

            // Check in-memory cache for local file base64 data
            if (Base64Cache.TryGetValue(pathOrUrl, out var cachedDataUri))
            {
                return cachedDataUri;
            }

            try
            {
                if (File.Exists(pathOrUrl))
                {
                    byte[] bytes = File.ReadAllBytes(pathOrUrl);
                    string ext = Path.GetExtension(pathOrUrl).ToLowerInvariant();
                    string mimeType = ext switch
                    {
                        ".png" => "image/png",
                        ".gif" => "image/gif",
                        ".webp" => "image/webp",
                        _ => "image/jpeg"
                    };

                    string dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";

                    if (Base64Cache.Count >= MaxCacheEntries)
                    {
                        Base64Cache.Clear();
                    }
                    Base64Cache[pathOrUrl] = dataUri;
                    return dataUri;
                }
                else
                {
                    // Cesta uložená v DB neexistuje na disku - dřív se to řešilo úplně potichu, což
                    // znemožňovalo dohledat, proč se karta v Blazoru zobrazuje s výchozí barvou
                    // místo fotky. Teď to jde aspoň vidět v Debug Output/adb logu.
                    Debug.WriteLine($"[ImageHelper] Soubor neexistuje: {pathOrUrl}");
                }
            }
            catch (Exception ex)
            {
                // Skutečná výjimka při čtení (uzamčený soubor, chybějící oprávnění...) - dřív úplně
                // tichý catch, teď aspoň vidět, co přesně selhalo a u kterého souboru.
                Debug.WriteLine($"[ImageHelper] Nepodařilo se načíst '{pathOrUrl}': {ex.Message}");
            }

            return pathOrUrl;
        }
    }
}