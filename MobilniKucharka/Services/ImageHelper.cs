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
            }
            catch
            {
                // Silently fallback if file cannot be read
            }

            return pathOrUrl;
        }
    }
}
