using SQLite;

namespace MobilniKucharka.Translation
{
    // Cache pro překlad vyhledávacích dotazů (RecipeSearchService.TranslateQueryToEnglishAsync).
    // Na rozdíl od RecipeTranslationCache (vázaná na konkrétní RecipeId) tahle cache není spojená
    // s žádným receptem - jen "tenhle český dotaz už jsme jednou přeložili do angličtiny, nemusíme
    // volat DeepL znovu, pokud ho uživatel zadá znovu". Směr je vždy jen cs->en (MealDB i
    // Spoonacular jsou anglická, opačný směr se pro dotazy nikdy nepoužívá).
    public class SearchQueryTranslationCache
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string OriginalText { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
    }
}