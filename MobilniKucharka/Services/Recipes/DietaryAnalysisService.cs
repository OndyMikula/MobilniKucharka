using MobilniKucharka.Services.Utilities;
using MobilniKucharka.Translation.UI;

namespace MobilniKucharka.Services.Recipes
{
    // Heuristická kontrola vegetariánství/veganství/bezlaktózové diety podle IngredientsRaw -
    // Recipe.DietaryFlags se prakticky nikdy spolehlivě nevyplňuje. Klíčová slova jsou jen česky,
    // anglický ekvivalent se bere z ui_translations_en.json přes UiTranslator - jeden zdroj pravdy
    // místo dvou ručně synchronizovaných seznamů.
    public static class DietaryAnalysisService
    {
        private static readonly string[] MeatFishKeywords =
        [
            "maso", "kuře", "kuřecí", "hovězí", "vepřové", "vepřová", "jehněčí", "krůtí", "kachní", "husí",
            "slanina", "šunka", "klobása", "salám", "párek", "ryba", "rybí", "losos", "tuňák", "treska",
            "kreveta", "krevety", "mušle", "chobotnice", "kalamár", "žralok", "kaviár", "ančovička",
            "želatina", "sádlo"
        ];

        private static readonly string[] DairyKeywords =
        [
            "mléko", "smetana", "máslo", "sýr", "jogurt", "tvaroh", "kefír", "eidam", "parmazán",
            "mozzarella", "ricotta", "mascarpone", "syrovátka"
        ];

        private static readonly string[] EggKeywords = ["vejce", "vaječný", "vaječkový", "žloutek", "bílek"];

        private static readonly string[] HoneyKeywords = ["med"];

        public static bool IsVegetarianCompatible(string ingredientsRaw) =>
            !ContainsAnyIngredientName(ingredientsRaw, MeatFishKeywords);

        public static bool IsVeganCompatible(string ingredientsRaw) =>
            !ContainsAnyIngredientName(ingredientsRaw, MeatFishKeywords) &&
            !ContainsAnyIngredientName(ingredientsRaw, DairyKeywords) &&
            !ContainsAnyIngredientName(ingredientsRaw, EggKeywords) &&
            !ContainsAnyIngredientName(ingredientsRaw, HoneyKeywords);

        public static bool IsLactoseFreeCompatible(string ingredientsRaw) =>
            !ContainsAnyIngredientName(ingredientsRaw, DairyKeywords);

        private static bool ContainsAnyIngredientName(string ingredientsRaw, string[] czechKeywords)
        {
            if (string.IsNullOrWhiteSpace(ingredientsRaw)) return false;

            var lines = ingredientsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string normalizedName = TextNormalizationHelper.NormalizeForComparison(line.Split('|')[0]);

                foreach (var keyword in czechKeywords)
                {
                    if (normalizedName.Contains(TextNormalizationHelper.NormalizeForComparison(keyword)))
                        return true;

                    string english = UiTranslator.TranslateToEnglishAlways(keyword);
                    string normalizedEn = TextNormalizationHelper.NormalizeForComparison(english);
                    if (normalizedEn.Length > 0 && normalizedName.Contains(normalizedEn))
                        return true;
                }
            }
            return false;
        }
    }
}