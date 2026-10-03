namespace MobilniKucharka.Services
{
    // Heuristicky odhadne, které spotřebiče (Trouba/Sporák/Konvice/Mikrovlnka - stejné čtyři
    // jako v onboardingu) recept potřebuje, podle textu kroků. Na rozdíl od DietaryAnalysisService
    // jde o krátké slovní tvary/fráze v přirozeném textu, ne izolovaná podstatná jména - bilingvní
    // seznamy jsou tu spolehlivější než překlad přes slovník jednotlivých slov.
    public static class RequiredEquipmentAnalysisService
    {
        private static readonly string[] OvenKeywords =
        [
            "trouba", "troubě", "troubu", "troubou", "pečte", "péct", "upečte", "upéct", "zapečte", "zapékejte", "zapékat",
            "oven", "preheat", "bake", "baking", "baked", "roast", "roasting", "broil"
        ];

        private static readonly string[] StoveKeywords =
        [
            "pánvi", "pánvích", "pánev", "pánve", "hrnci", "hrnec", "hrnce", "kastrolu", "kastrol",
            "smažte", "smažit", "osmažte", "restujte", "restovat", "orestujte", "duste", "dusit",
            "vařte", "vařit", "povařte", "plotýnka", "plotýnce", "sporák", "sporáku",
            "pan", "skillet", "pot", "saucepan", "fry", "fried", "frying", "sauté", "saute",
            "simmer", "simmering", "boil", "boiling", "stovetop", "hob", "stir-fry", "stir fry"
        ];

        private static readonly string[] KettleKeywords = ["konvice", "konvici", "konvicí", "kettle"];

        private static readonly string[] MicrowaveKeywords =
        [
            "mikrovlnka", "mikrovlnce", "mikrovlnnou", "microwave"
        ];

        public static List<string> InferRequiredEquipment(string combinedStepsText)
        {
            var required = new List<string>();
            if (string.IsNullOrWhiteSpace(combinedStepsText)) return required;

            string normalized = TextNormalizationHelper.NormalizeForComparison(combinedStepsText);

            if (ContainsAny(normalized, OvenKeywords)) required.Add("Trouba");
            if (ContainsAny(normalized, StoveKeywords)) required.Add("Sporák");
            if (ContainsAny(normalized, KettleKeywords)) required.Add("Konvice");
            if (ContainsAny(normalized, MicrowaveKeywords)) required.Add("Mikrovlnka");

            return required;
        }

        private static bool ContainsAny(string normalizedText, string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (normalizedText.Contains(TextNormalizationHelper.NormalizeForComparison(keyword)))
                    return true;
            }
            return false;
        }
    }
}