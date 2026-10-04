namespace MobilniKucharka.Services.Cache
{
    // Cache posledního seznamu na RecipesPage (celý plán, nebo lokální hledání podle CachedKey).
    // Přežije navigaci, ne restart appky. Invaliduje se centrálně v BudgetPlannerService a v
    // App.ResetDatabase u změn, které karta zobrazuje (recept, cena/jednotka/propojení suroviny).
    // Hodnocení, oblíbené a záložky seznam neovlivňují - tam se NEinvaliduje.
    public static class RecipeListCache
    {
        public static List<RecipeWithCost>? CachedPlan { get; set; }
        public static string CachedKey { get; set; } = string.Empty;
        public static string CachedSearchText { get; set; } = string.Empty;
        public static bool CachedFilterByPreferences { get; set; } = true;

        // Roste s každou invalidací - načítání, které začalo před ní, nezapíše zastaralý výsledek.
        public static int Version { get; private set; }

        public static void Invalidate()
        {
            CachedPlan = null;
            Version++;
        }
    }
}