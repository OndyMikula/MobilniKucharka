using MobilniKucharka.Services.Recipes;

namespace MobilniKucharka.Services.Cache
{
    // Dočasný stav SearchPage přežívající mezi navigacemi (ne restart appky) - dotaz i poslední
    // výsledky zůstanou, dokud uživatel znovu nevyhledá nebo appku nerestartuje. Na rozdíl od
    // RecipeListCache tu není potřeba žádná invalidace - výsledky internetového hledání se stejně
    // nemění samy od sebe.
    public static class SearchPageState
    {
        public static string LastQuery { get; set; } = string.Empty;
        public static bool LastFilterByPreferences { get; set; }
        public static List<ExternalRecipeSearchResult>? LastResults { get; set; }
        public static bool HasSearched { get; set; }
    }
}