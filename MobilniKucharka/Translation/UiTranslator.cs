using System.Text;
using System.Text.Json;

namespace MobilniKucharka.Translation
{
    // Gettext-styl lokalizace: český text JE klíč. Žádné resx, žádné abstraktní klíče jako Label_Save.
    // Tr("Uložit") vrátí "Save" v anglickém režimu, pokud je překlad součástí APK; jinak vrátí
    // původní český text. Překlady se připravují vývojářem před vydáním, nikdy přes API uživatele.
    public static class UiTranslator
    {
        private const string BundledFileName = "ui_translations_en.json"; // Resources/Raw, součást buildu

        private static readonly Dictionary<string, string> _aliases = [];
        private static bool _isLoaded = false;

        // Zavolej jednou při startu appky (viz App.xaml.cs). Dokud nedoběhne, Tr() jede na fallbacku
        // (vrací český text) - žádný pád, jen krátké okno bez překladu hned po studeném startu.
        public static async Task InitializeAsync()
        {
            if (_isLoaded) return;

            // "Z výroby" dodaný slovník - jede v každém buildu, bez internetu.
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(BundledFileName).ConfigureAwait(false);
                using var reader = new StreamReader(stream);
                string bundledJson = await reader.ReadToEndAsync().ConfigureAwait(false);
                var bundled = JsonSerializer.Deserialize<Dictionary<string, string>>(bundledJson);
                if (bundled != null)
                {
                    foreach (var kvp in bundled)
                    {
                        _aliases[kvp.Key] = kvp.Value;
                        _aliases[NormalizeLegacyMojibake(kvp.Key)] = NormalizeLegacyMojibake(kvp.Value);
                    }
                }
            }
            catch
            {
                // Soubor ještě nemusí existovat (první spuštění po zavedení téhle funkce) - OK.
            }

            _isLoaded = true;
        }

        // Hlavní vstupní bod - volej z XAML přes {loc:Tr '...'} i přímo z code-behind.
        public static string Tr(string csText)
        {
            if (string.IsNullOrWhiteSpace(csText)) return csText;

            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            if (currentLang != "en") return csText; // čeština je default, není co překládat

            if (_aliases.TryGetValue(csText, out var alias) && !string.IsNullOrWhiteSpace(alias))
                return alias;

            string legacyKey = NormalizeLegacyMojibake(csText);
            if (!string.Equals(legacyKey, csText, StringComparison.Ordinal) &&
                _aliases.TryGetValue(legacyKey, out var legacyAlias) &&
                !string.IsNullOrWhiteSpace(legacyAlias))
            {
                return legacyAlias;
            }

            return csText;
        }

        // Počtem řízené skloňování (1 / 2-4 / 5+ pro CS, 1 / ostatní pro EN).
        // Nejde přes DeepL - gramatická shoda s číslovkou je pravidlo, ne "překlad věty",
        // takže slova pro obě jazykové varianty i obě skloňovací kategorie zadáváš explicitně.
        // Příklad: TrCount(3, "člověk", "lidé", "lidí", "person", "people") => "3 lidé" (CS) / "3 people" (EN)
        public static string TrCount(int count, string csSingular, string csFew, string csMany, string enSingular, string enPlural)
        {
            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");

            if (currentLang == "en")
                return count == 1 ? $"{count} {enSingular}" : $"{count} {enPlural}";

            if (count == 1) return $"{count} {csSingular}";
            if (count >= 2 && count <= 4) return $"{count} {csFew}";
            return $"{count} {csMany}";
        }

        // Konkrétní zkratka pro "počet lidí" - přesně podle zadání:
        // 1 -> "1 člověk" / "1 person", 3 -> "3 lidé" / "3 people", 5 -> "5 lidí" / "5 people"
        public static string TrPeopleCount(int count) =>
            TrCount(count, csSingular: "člověk", csFew: "lidé", csMany: "lidí", enSingular: "person", enPlural: "people");

        private static string NormalizeLegacyMojibake(string text)
        {
            try
            {
                var bytes = Encoding.GetEncoding(1252).GetBytes(text);
                string decoded = Encoding.UTF8.GetString(bytes);
                return string.IsNullOrWhiteSpace(decoded) ? text : decoded;
            }
            catch
            {
                return text;
            }
        }
    }
}
