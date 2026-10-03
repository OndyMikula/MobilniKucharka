namespace MobilniKucharka.Translation
{
    // DeepL bez kontextu receptu občas přeloží krátké slovo doslovně jinak, než dává smysl
    // v kuchyni (např. "Oil" -> "Ropa", surová ropa, místo "Olej"). Pro pár takových
    // vyzkoušeně špatných případů appka místo DeepL použije tenhle pevný slovník.
    public static class IngredientTranslationOverrides
    {
        private static readonly Dictionary<string, string> CsToEn = new(StringComparer.OrdinalIgnoreCase)
        {
            ["olej"] = "Oil",
        };

        private static readonly Dictionary<string, string> EnToCs = new(StringComparer.OrdinalIgnoreCase)
        {
            ["oil"] = "Olej",
        };

        public static string? TryGet(string text, string fromLang)
        {
            var dict = fromLang == "cs" ? CsToEn : EnToCs;
            return dict.TryGetValue(text.Trim(), out var value) ? value : null;
        }
    }
}