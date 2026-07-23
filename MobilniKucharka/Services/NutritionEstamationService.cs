namespace MobilniKucharka.Services
{
    public static class NutritionEstimationService
    {
        // Hodnoty na 100 g / 100 ml (hrubý odhad z veřejně známých nutričních tabulek)
        private static readonly Dictionary<string, (double Protein, double Carbs, double Fat, double Sugar)> NutritionPer100 = new()
        {
            ["kuřecí"] = (23, 0, 3, 0),
            ["chicken"] = (23, 0, 3, 0),
            ["hovězí"] = (26, 0, 15, 0),
            ["beef"] = (26, 0, 15, 0),
            ["vepřové"] = (21, 0, 14, 0),
            ["pork"] = (21, 0, 14, 0),
            ["losos"] = (20, 0, 13, 0),
            ["salmon"] = (20, 0, 13, 0),
            ["treska"] = (18, 0, 0.7, 0),
            ["cod"] = (18, 0, 0.7, 0),
            ["clam"] = (24, 5, 2, 0),
            ["mušle"] = (24, 5, 2, 0),
            ["škeble"] = (24, 5, 2, 0),
            ["vejce"] = (13, 1, 11, 1),
            ["egg"] = (13, 1, 11, 1),
            ["mléko"] = (3.4, 5, 3.6, 5),
            ["milk"] = (3.4, 5, 3.6, 5),
            ["smetan"] = (2, 3, 35, 3),
            ["cream"] = (2, 3, 35, 3),
            ["sýr"] = (25, 1, 33, 0.5),
            ["cheese"] = (25, 1, 33, 0.5),
            ["máslo"] = (0.9, 0.1, 81, 0.1),
            ["butter"] = (0.9, 0.1, 81, 0.1),
            ["olej"] = (0, 0, 100, 0),
            ["oil"] = (0, 0, 100, 0),
            ["mouk"] = (10, 76, 1, 0.3),
            ["flour"] = (10, 76, 1, 0.3),
            ["cukr"] = (0, 100, 0, 100),
            ["sugar"] = (0, 100, 0, 100),
            ["rýž"] = (7, 80, 0.6, 0.1),
            ["rice"] = (7, 80, 0.6, 0.1),
            ["těstovin"] = (13, 75, 1.5, 3),
            ["špaget"] = (13, 75, 1.5, 3),
            ["pasta"] = (13, 75, 1.5, 3),
            ["spaghetti"] = (13, 75, 1.5, 3),
            ["brambor"] = (2, 17, 0.1, 0.8),
            ["potato"] = (2, 17, 0.1, 0.8),
            ["cibul"] = (1.1, 9, 0.1, 4.2),
            ["onion"] = (1.1, 9, 0.1, 4.2),
            ["česnek"] = (6.4, 33, 0.5, 1),
            ["garlic"] = (6.4, 33, 0.5, 1),
            ["rajč"] = (0.9, 3.9, 0.2, 2.6),
            ["tomato"] = (0.9, 3.9, 0.2, 2.6),
            ["mrkev"] = (0.9, 10, 0.2, 4.7),
            ["carrot"] = (0.9, 10, 0.2, 4.7),
            ["paprik"] = (1, 6, 0.3, 4.2),
            ["pepper"] = (1, 6, 0.3, 4.2),
            ["fazol"] = (8, 20, 0.5, 1),
            ["bean"] = (8, 20, 0.5, 1),
            ["čočk"] = (9, 20, 0.4, 1.8),
            ["lentil"] = (9, 20, 0.4, 1.8),
            ["cizrn"] = (8.9, 27, 2.6, 4.8),
            ["chickpea"] = (8.9, 27, 2.6, 4.8),
            ["vývar"] = (1, 1, 0.5, 0.3),
            ["bujón"] = (1, 1, 0.5, 0.3),
            ["stock"] = (1, 1, 0.5, 0.3),
            ["broth"] = (1, 1, 0.5, 0.3),
            ["jogurt"] = (3.5, 4.7, 3.3, 4.7),
            ["yogurt"] = (3.5, 4.7, 3.3, 4.7),
            ["med"] = (0.3, 82, 0, 82),
            ["honey"] = (0.3, 82, 0, 82),
            ["chléb"] = (9, 49, 3.2, 5),
            ["bread"] = (9, 49, 3.2, 5),
            ["salát"] = (1.4, 2.9, 0.2, 0.8),
            ["lettuce"] = (1.4, 2.9, 0.2, 0.8),
            ["houb"] = (3.1, 3.3, 0.3, 2),
            ["mushroom"] = (3.1, 3.3, 0.3, 2),
            ["citron"] = (1.1, 9.3, 0.3, 2.5),
            ["lemon"] = (1.1, 9.3, 0.3, 2.5),
            ["mandl"] = (21, 22, 50, 4),
            ["almond"] = (21, 22, 50, 4),
        };

        public static (double Protein, double Carbs, double Fat, double Sugar) EstimateNutrition(List<(string Name, string Amount)> ingredients)
        {
            double totalProtein = 0, totalCarbs = 0, totalFat = 0, totalSugar = 0;

            foreach (var ing in ingredients)
            {
                var match = FindNutritionMatch(ing.Name);
                if (match == null) continue; // neznámá ingredience -> raději přeskočit než hádat naslepo

                double grams = ParseAmountToGrams(ing.Amount);
                double factor = grams / 100.0;

                totalProtein += match.Value.Protein * factor;
                totalCarbs += match.Value.Carbs * factor;
                totalFat += match.Value.Fat * factor;
                totalSugar += match.Value.Sugar * factor;
            }

            return (Math.Round(totalProtein, 1), Math.Round(totalCarbs, 1), Math.Round(totalFat, 1), Math.Round(totalSugar, 1));
        }

        private static (double Protein, double Carbs, double Fat, double Sugar)? FindNutritionMatch(string ingredientName)
        {
            string normalized = ingredientName.ToLowerInvariant();

            string? bestKey = null;
            foreach (var key in NutritionPer100.Keys)
            {
                if (normalized.Contains(key) && (bestKey == null || key.Length > bestKey.Length))
                    bestKey = key;
            }

            return bestKey != null ? NutritionPer100[bestKey] : null;
        }

        private static double ParseAmountToGrams(string amountText)
        {
            if (string.IsNullOrWhiteSpace(amountText)) return 100; // neznámé množství -> střední odhad jedné porce

            string text = amountText.Trim().ToLowerInvariant();
            var match = System.Text.RegularExpressions.Regex.Match(text, @"^(\d+(?:[.,]\d+)?)");

            double quantity = 1;
            string rest = text;

            if (match.Success)
            {
                quantity = double.Parse(match.Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
                rest = text[match.Length..].Trim();
            }

            if (rest.Contains("kg")) return quantity * 1000;
            if (rest.Contains('g') && !rest.Contains("gal")) return quantity;
            if (rest.Contains("ml")) return quantity;
            if (rest.Contains('l') && !rest.Contains("small") && !rest.Contains("large")) return quantity * 1000;

            if (rest.Contains("tbsp") || rest.Contains("lžíce") || rest.Contains("polévkov")) return quantity * 15;
            if (rest.Contains("tsp") || rest.Contains("lžička") || rest.Contains("čajov")) return quantity * 5;
            if (rest.Contains("cup") || rest.Contains("hrnek") || rest.Contains("šálek")) return quantity * 240;

            if (rest.Contains("small") || rest.Contains("malý") || rest.Contains("malá")) return quantity * 50;
            if (rest.Contains("medium") || rest.Contains("střední")) return quantity * 100;
            if (rest.Contains("large") || rest.Contains("velký") || rest.Contains("velká")) return quantity * 150;

            if (match.Success && string.IsNullOrWhiteSpace(rest)) return quantity * 60; // holé číslo (ks) -> hrubý odhad na kus

            return 15; // zcela neurčité ("topping", "to taste"...) -> malý nominální odhad
        }
    }
}