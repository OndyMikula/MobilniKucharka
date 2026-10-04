using System.Globalization;
using System.Text.RegularExpressions;

namespace MobilniKucharka.Services
{
    public static partial class NutritionEstimationService
    {
        // Na 100 g: bílkoviny, sacharidy, tuky, cukry. Delší klíč vyhrává, klíč se hledá od začátku slova
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

            // Bez živin, ale počítá se jako rozpoznané
            ["water"] = (0, 0, 0, 0),
            ["voda"] = (0, 0, 0, 0),
            ["salt"] = (0, 0, 0, 0),
            ["sůl"] = (0, 0, 0, 0),
            ["black pepper"] = (10, 64, 3, 0.6),
            ["pepř"] = (10, 64, 3, 0.6),

            // Sladké a pečení
            ["milk chocolate"] = (7.6, 59, 30, 56),
            ["dark chocolate"] = (7.8, 46, 43, 24),
            ["white chocolate"] = (5.9, 59, 32, 59),
            ["chocolate"] = (7, 58, 31, 50),
            ["čokolád"] = (7, 58, 31, 50),
            ["cocoa"] = (20, 58, 14, 2),
            ["kakao"] = (20, 58, 14, 2),
            ["mars bar"] = (4, 69, 17, 60),
            ["snickers"] = (9, 60, 25, 49),
            ["marshmallow"] = (2, 81, 0, 58),
            ["caramel"] = (3, 75, 9, 60),
            ["condensed milk"] = (8, 55, 9, 55),
            ["syrup"] = (0, 70, 0, 62),
            ["sirup"] = (0, 70, 0, 62),
            ["icing sugar"] = (0, 100, 0, 98),
            ["powdered sugar"] = (0, 100, 0, 98),
            ["cornflour"] = (0.3, 91, 0.1, 0),
            ["cornstarch"] = (0.3, 91, 0.1, 0),
            ["raisin"] = (3, 79, 0.5, 59),
            ["rozink"] = (3, 79, 0.5, 59),
            ["rice krispies"] = (6, 87, 1, 10),
            ["cornflakes"] = (7, 84, 1, 8),
            ["corn flakes"] = (7, 84, 1, 8),
            ["oats"] = (13, 66, 7, 1),
            ["ovesn"] = (13, 66, 7, 1),
            ["granola"] = (10, 64, 15, 20),

            // Ořechy, kokos, mléčné varianty
            ["peanut butter"] = (25, 20, 50, 9),
            ["arašídové máslo"] = (25, 20, 50, 9),
            ["peanut oil"] = (0, 0, 100, 0),
            ["peanut"] = (26, 16, 49, 4),
            ["arašíd"] = (26, 16, 49, 4),
            ["walnut"] = (15, 14, 65, 3),
            ["cashew"] = (18, 30, 44, 6),
            ["pistachio"] = (20, 28, 45, 8),
            ["hazelnut"] = (15, 17, 61, 4),
            ["coconut oil"] = (0, 0, 100, 0),
            ["coconut milk"] = (2.3, 6, 24, 3),
            ["coconut"] = (3, 15, 33, 6),
            ["kokos"] = (3, 15, 33, 6),
            ["almond milk"] = (0.4, 3, 1.1, 2),
            ["cream cheese"] = (6, 4, 34, 3),
            ["sour cream"] = (3, 4, 20, 3),
            ["ice cream"] = (3.5, 24, 11, 21),

            // Maso, ryby, tofu
            ["bacon"] = (37, 1.4, 42, 0),
            ["slanin"] = (37, 1.4, 42, 0),
            ["ham"] = (18, 1.5, 5, 1),
            ["šunk"] = (18, 1.5, 5, 1),
            ["sausage"] = (13, 2, 28, 1),
            ["klobás"] = (13, 2, 28, 1),
            ["turkey"] = (29, 0, 7, 0),
            ["lamb"] = (25, 0, 21, 0),
            ["jehněč"] = (25, 0, 21, 0),
            ["prawn"] = (24, 0.2, 0.3, 0),
            ["shrimp"] = (24, 0.2, 0.3, 0),
            ["krevet"] = (24, 0.2, 0.3, 0),
            ["tuna"] = (29, 0, 1, 0),
            ["mince"] = (20, 0, 15, 0),
            ["tofu"] = (8, 2, 5, 0.6),

            // Zelenina a ovoce
            ["spinach"] = (2.9, 3.6, 0.4, 0.4),
            ["broccoli"] = (2.8, 7, 0.4, 1.7),
            ["cucumber"] = (0.7, 3.6, 0.1, 1.7),
            ["cabbage"] = (1.3, 5.8, 0.1, 3.2),
            ["zucchini"] = (1.2, 3.1, 0.3, 2.5),
            ["courgette"] = (1.2, 3.1, 0.3, 2.5),
            ["eggplant"] = (1, 6, 0.2, 3.5),
            ["aubergine"] = (1, 6, 0.2, 3.5),
            ["avocado"] = (2, 9, 15, 0.7),
            ["banana"] = (1.1, 23, 0.3, 12),
            ["apple"] = (0.3, 14, 0.2, 10),
            ["sweet potato"] = (1.6, 20, 0.1, 4.2),
            ["pumpkin"] = (1, 6.5, 0.1, 2.8),
            ["sweetcorn"] = (3.2, 19, 1.2, 6),

            // Spíž
            ["soy sauce"] = (8, 5, 0.6, 1),
            ["tomato paste"] = (4.3, 19, 0.5, 12),
            ["tomato puree"] = (4.3, 19, 0.5, 12),
            ["ketchup"] = (1, 27, 0.1, 22),
            ["mayonnaise"] = (1, 3, 75, 2),
            ["vinegar"] = (0, 0.9, 0, 0.4),
            ["ocet"] = (0, 0.9, 0, 0.4),
            ["wine"] = (0.1, 2.6, 0, 1),
            ["tortilla"] = (8, 50, 8, 2),
            ["pita"] = (9, 55, 1.2, 1),
            ["noodle"] = (12, 72, 3, 3),
            ["couscous"] = (13, 73, 0.6, 1),
            ["quinoa"] = (14, 64, 6, 0),
            ["buckwheat"] = (13, 72, 3, 0),
            ["pohank"] = (13, 72, 3, 0),
            ["semolina"] = (13, 73, 1, 2),
            ["coconut sugar"] = (0, 100, 0, 90),
            ["flax egg"] = (2.5, 3.9, 5.8, 0.2),
            ["flax"] = (18, 29, 42, 1.6),
            ["chia"] = (17, 42, 31, 0),
        };

        // Podíl rozpoznané váhy, pod kterým se odhad raději nezobrazí
        private const double MinKnownWeightShare = 0.7;

        // g na ml pro sypké suroviny, ostatní = 1.0 (hrnek/lžíce nejsou voda)
        private static readonly Dictionary<string, double> GramsPerMl = new()
        {
            ["flour"] = 0.53,
            ["cornflour"] = 0.53,
            ["mouk"] = 0.53,
            ["sugar"] = 0.85,
            ["cukr"] = 0.85,
            ["coconut sugar"] = 0.7,
            ["icing sugar"] = 0.5,
            ["powdered sugar"] = 0.5,
            ["cocoa"] = 0.36,
            ["kakao"] = 0.36,
            ["oats"] = 0.37,
            ["ovesn"] = 0.37,
            ["granola"] = 0.4,
            ["rice"] = 0.8,
            ["rýž"] = 0.8,
            ["rice vinegar"] = 1.0,
            ["rice krispies"] = 0.12,
            ["cornflakes"] = 0.12,
            ["corn flakes"] = 0.12,
            ["butter"] = 0.95,
            ["máslo"] = 0.95,
            ["peanut butter"] = 1.05,
            ["oil"] = 0.92,
            ["olej"] = 0.92,
            ["honey"] = 1.4,
            ["syrup"] = 1.35,
            ["almond"] = 0.45,
            ["almond milk"] = 1.03,
            ["walnut"] = 0.5,
            ["coconut"] = 0.4,
            ["coconut milk"] = 1.0,
            ["coconut oil"] = 0.92,
            ["raisin"] = 0.7,
            ["couscous"] = 0.7,
            ["quinoa"] = 0.7,
            ["semolina"] = 0.65,
            ["salt"] = 1.2
        };

        private enum UnitKind { Weight, Volume, Piece }

        // Gramy na jednotku; ostatní jednotky pro "kus"
        private static readonly (string Prefix, double Grams)[] PieceUnits =
        [
            ("clove", 5), ("stroužk", 5),
            ("slice", 25), ("plát", 25),
            ("handful", 30), ("hrst", 30),
            ("bunch", 50), ("svaz", 50),
            ("sprig", 2), ("snítk", 2),
            ("leaf", 1), ("leaves", 1),
            ("plechov", 400)
        ];

        private static readonly Dictionary<char, double> UnicodeFractions = new()
        {
            ['½'] = 0.5,
            ['¼'] = 0.25,
            ['¾'] = 0.75,
            ['⅓'] = 1.0 / 3,
            ['⅔'] = 2.0 / 3,
            ['⅛'] = 0.125
        };

        [GeneratedRegex(@"^(\d+)\s+(\d+)/(\d+)")]
        private static partial Regex MixedFractionRegexGen();

        [GeneratedRegex(@"^(\d+)/(\d+)")]
        private static partial Regex SimpleFractionRegexGen();

        [GeneratedRegex(@"^(?:(\d+)\s?)?([½¼¾⅓⅔⅛])")]
        private static partial Regex UnicodeFractionRegexGen();

        [GeneratedRegex(@"^\d+(?:[.,]\d+)?")]
        private static partial Regex LeadingNumberRegexGen();

        [GeneratedRegex(@"^\p{L}+")]
        private static partial Regex UnitTokenRegexGen();

        public static (double Protein, double Carbs, double Fat, double Sugar) EstimateNutrition(List<(string Name, string Amount)> ingredients)
        {
            double totalProtein = 0, totalCarbs = 0, totalFat = 0, totalSugar = 0;
            double knownGrams = 0, unknownGrams = 0;

            foreach (var (Name, Amount) in ingredients)
            {
                double grams = ParseAmountToGrams(Amount, 60, FindGramsPerMl(Name));
                var match = FindNutritionMatch(Name);
                System.Diagnostics.Debug.WriteLine($"[Nutrition] {Name} | {Amount} -> {grams:0.#} g, {(match == null ? "NO MATCH" : "ok")}");

                if (match == null)
                {
                    unknownGrams += grams;
                    continue;
                }

                knownGrams += grams;
                double factor = grams / 100.0;

                totalProtein += match.Value.Protein * factor;
                totalCarbs += match.Value.Carbs * factor;
                totalFat += match.Value.Fat * factor;
                totalSugar += match.Value.Sugar * factor;
            }

            // Moc nerozpoznaných surovin = raději žádné číslo než zavádějící
            double total = knownGrams + unknownGrams;
            if (total <= 0 || knownGrams / total < MinKnownWeightShare) return (0, 0, 0, 0);

            return (Math.Round(totalProtein, 1), Math.Round(totalCarbs, 1), Math.Round(totalFat, 1), Math.Round(totalSugar, 1));
        }

        private static (double Protein, double Carbs, double Fat, double Sugar)? FindNutritionMatch(string ingredientName)
        {
            string normalized = ingredientName.ToLowerInvariant();

            string? bestKey = null;
            foreach (var key in NutritionPer100.Keys)
            {
                if ((bestKey == null || key.Length > bestKey.Length) && ContainsAtWordStart(normalized, key))
                    bestKey = key;
            }

            return bestKey != null ? NutritionPer100[bestKey] : null;
        }

        // Ordinal, ať "ch" v češtině nerozbije porovnání
        private static bool ContainsAtWordStart(string text, string key)
        {
            int i = text.IndexOf(key, StringComparison.Ordinal);
            while (i >= 0)
            {
                if (i == 0 || !char.IsLetter(text[i - 1])) return true;
                i = text.IndexOf(key, i + 1, StringComparison.Ordinal);
            }
            return false;
        }

        private static double FindGramsPerMl(string ingredientName)
        {
            string normalized = ingredientName.ToLowerInvariant();

            string? bestKey = null;
            foreach (var key in GramsPerMl.Keys)
            {
                if ((bestKey == null || key.Length > bestKey.Length) && ContainsAtWordStart(normalized, key))
                    bestKey = key;
            }

            return bestKey != null ? GramsPerMl[bestKey] : 1.0;
        }

        // Přečte číslo na začátku: 2, 2.5, 1/2, 1 1/2, ½, 1½
        public static bool TryReadLeadingQuantity(string text, out double quantity, out int length)
        {
            quantity = 0;
            length = 0;

            var mixed = MixedFractionRegexGen().Match(text);
            if (mixed.Success)
            {
                quantity = ParseInt(mixed.Groups[1].Value) + SafeDivide(mixed.Groups[2].Value, mixed.Groups[3].Value);
                length = mixed.Length;
                return true;
            }

            var simple = SimpleFractionRegexGen().Match(text);
            if (simple.Success)
            {
                quantity = SafeDivide(simple.Groups[1].Value, simple.Groups[2].Value);
                length = simple.Length;
                return true;
            }

            var unicode = UnicodeFractionRegexGen().Match(text);
            if (unicode.Success)
            {
                double whole = unicode.Groups[1].Success ? ParseInt(unicode.Groups[1].Value) : 0;
                quantity = whole + UnicodeFractions[unicode.Groups[2].Value[0]];
                length = unicode.Length;
                return true;
            }

            var plain = LeadingNumberRegexGen().Match(text);
            if (plain.Success)
            {
                quantity = double.Parse(plain.Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                length = plain.Length;
                return true;
            }

            return false;
        }

        private static double ParseInt(string value) => double.Parse(value, CultureInfo.InvariantCulture);

        private static double SafeDivide(string numerator, string denominator)
        {
            double den = ParseInt(denominator);
            return den == 0 ? 0 : ParseInt(numerator) / den;
        }

        private static bool Starts(string token, string prefix) => token.StartsWith(prefix, StringComparison.Ordinal);

        // Weight = gramy, Volume = ml, Piece = gramy na kus (0 = výchozí váha kusu, -1 = neznámé slovo)
        private static (UnitKind Kind, double Factor) ResolveUnit(string rest)
        {
            var tokenMatch = UnitTokenRegexGen().Match(rest);
            string token = tokenMatch.Success ? tokenMatch.Value : string.Empty;

            switch (token)
            {
                case "": case "x": case "ks": return (UnitKind.Piece, 0);
                case "g": case "gr": return (UnitKind.Weight, 1);
                case "kg": return (UnitKind.Weight, 1000);
                case "oz": return (UnitKind.Weight, 28.35);
                case "lb": case "lbs": return (UnitKind.Weight, 453.6);
                case "ml": return (UnitKind.Volume, 1);
                case "dl": return (UnitKind.Volume, 100);
                case "l": return (UnitKind.Volume, 1000);
                case "can": case "cans": case "tin": case "tins": return (UnitKind.Piece, 400);
            }

            if (Starts(token, "kus")) return (UnitKind.Piece, 0);
            if (Starts(token, "gram")) return (UnitKind.Weight, 1);
            if (Starts(token, "kilo")) return (UnitKind.Weight, 1000);
            if (Starts(token, "ounce")) return (UnitKind.Weight, 28.35);
            if (Starts(token, "pound")) return (UnitKind.Weight, 453.6);
            if (Starts(token, "millil") || Starts(token, "mililit")) return (UnitKind.Volume, 1);
            if (Starts(token, "lit")) return (UnitKind.Volume, 1000);

            if (Starts(token, "tbs") || Starts(token, "tablesp") || Starts(token, "lží") || Starts(token, "polévkov")) return (UnitKind.Volume, 15);
            if (Starts(token, "tsp") || Starts(token, "teasp") || Starts(token, "lži") || Starts(token, "čajov")) return (UnitKind.Volume, 5);
            if (Starts(token, "cup") || Starts(token, "hrn") || Starts(token, "šál")) return (UnitKind.Volume, 240);

            if (Starts(token, "pinch") || Starts(token, "špetk")) return (UnitKind.Weight, 0.4);
            if (Starts(token, "dash")) return (UnitKind.Weight, 0.6);

            if (Starts(token, "small") || Starts(token, "mal")) return (UnitKind.Piece, 50);
            if (Starts(token, "medium") || Starts(token, "střed")) return (UnitKind.Piece, 100);
            if (Starts(token, "large") || Starts(token, "velk")) return (UnitKind.Piece, 150);

            foreach (var (prefix, grams) in PieceUnits)
            {
                if (Starts(token, prefix)) return (UnitKind.Piece, grams);
            }

            return (UnitKind.Piece, -1);
        }

        public static double ParseAmountToGrams(string amountText, double defaultPieceWeight = 60, double gramsPerMl = 1.0)
        {
            if (string.IsNullOrWhiteSpace(amountText)) return 100;

            string text = amountText.Trim().ToLowerInvariant();

            bool hasNumber = TryReadLeadingQuantity(text, out double quantity, out int length);
            if (!hasNumber)
            {
                quantity = 1;
                length = 0;
            }

            var (kind, factor) = ResolveUnit(text[length..].Trim());

            if (kind == UnitKind.Volume) return quantity * factor * gramsPerMl;
            if (kind == UnitKind.Weight) return quantity * factor;
            if (factor > 0) return quantity * factor;

            return hasNumber ? quantity * defaultPieceWeight : 15;
        }

        public static double? TryParseLeadingQuantity(string amountText, double defaultPieceWeight = 60)
        {
            if (string.IsNullOrWhiteSpace(amountText)) return null;

            string text = amountText.Trim().ToLowerInvariant();
            if (!TryReadLeadingQuantity(text, out _, out _) && !text.Any(char.IsDigit)) return null;

            return ParseAmountToGrams(text, defaultPieceWeight);
        }

        public static double? ConvertToProductUnit(string amountText, string productUnit, double defaultPieceWeight = 60)
        {
            if (string.IsNullOrWhiteSpace(amountText)) return null;

            string text = amountText.Trim().ToLowerInvariant();
            if (!TryReadLeadingQuantity(text, out double quantity, out int length)) return null;

            var (kind, factor) = ResolveUnit(text[length..].Trim());

            double? weightGrams = kind == UnitKind.Weight ? quantity * factor : null;
            double? volumeMl = kind == UnitKind.Volume ? quantity * factor : null;
            bool isPiece = kind == UnitKind.Piece;
            double pieceGrams = isPiece ? quantity * (factor > 0 ? factor : defaultPieceWeight) : 0;

            return productUnit switch
            {
                "g" => weightGrams ?? volumeMl ?? (isPiece ? pieceGrams : null),
                "ml" => volumeMl ?? weightGrams ?? (isPiece ? pieceGrams : null),
                "ks" => isPiece ? quantity : null,
                _ => weightGrams ?? volumeMl
            };
        }

        public static string DetectUnitFamily(string amountText)
        {
            if (string.IsNullOrWhiteSpace(amountText)) return "g";

            string text = amountText.Trim().ToLowerInvariant();
            int start = TryReadLeadingQuantity(text, out _, out int length) ? length : 0;

            var (kind, factor) = ResolveUnit(text[start..].Trim());

            return kind switch
            {
                UnitKind.Weight => "g",
                UnitKind.Volume => "ml",
                _ => factor < 0 ? "g" : "ks"
            };
        }
    }
}