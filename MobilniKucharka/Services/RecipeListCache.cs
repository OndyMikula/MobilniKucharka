namespace MobilniKucharka.Services
{
    // Cache posledního seznamu na RecipesPage, jen za běhu appky
    public static class RecipeListCache
    {
        public static List<RecipeWithCost>? CachedPlan { get; set; }
        public static string CachedKey { get; set; } = string.Empty;
        public static string CachedSearchText { get; set; } = string.Empty;
        public static bool CachedFilterByPreferences { get; set; } = true;

        // Roste s každou invalidací, starší načítání nezapíše zastaralá data
        public static int Version { get; private set; }

        public static void Invalidate()
        {
            CachedPlan = null;
            Version++;
        }
    }
}