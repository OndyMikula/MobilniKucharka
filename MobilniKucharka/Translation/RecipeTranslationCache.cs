using SQLite;

namespace MobilniKucharka.Translation
{
    // Cache pro překlady polí, která na Recipe existují jen jednou (DescriptionText, IngredientsRaw) -
    // Name_CS/EN a Steps_CS/EN mají vlastní sloupce přímo na Recipe, ty tenhle cache nepotřebují.
    // Díky tomu se DeepL volá jen jednou za dvojici (recept, pole, jazyk) navždy - další přepnutí jazyka
    // u stejného receptu bere text odsud, ne z nového API callu.
    public class RecipeTranslationCache
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public string FieldName { get; set; } = string.Empty;    // "DescriptionText" nebo "IngredientsRaw"
        public string LanguageCode { get; set; } = string.Empty; // "cs" nebo "en"
        public string TranslatedText { get; set; } = string.Empty;
    }
}