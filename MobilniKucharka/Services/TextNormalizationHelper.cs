using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MobilniKucharka.Services
{
    // Sdílené porovnávání textu ignorující velikost písmen, diakritiku, pomlčky/mezery a
    // interpunkci navíc (závorky, uvozovky...) - "sůl"/"Sul", "All-purpose flour"/"All purpose
    // flour" i "sezamová semínka"/"sezamová semínka)" musí vyjít jako stejné slovo.
    public static class TextNormalizationHelper
    {
        public static string NormalizeForComparison(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string decomposed = text.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (c is '-' or '–' or '—')
                {
                    sb.Append(' ');
                    continue;
                }

                if (char.IsPunctuation(c) || char.IsSymbol(c))
                    continue;

                sb.Append(c);
            }

            string result = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
            result = Regex.Replace(result, @"\s+", " ").Trim();
            return result;
        }

        // Rozdělí "Paprika (koření)" na základ "Paprika" (normalizovaný) + příznak, že závorka
        // byla přítomná. Obsah závorky se úmyslně zahazuje - "Paprika (koření)"/"Paprika (na
        // koření)"/"Paprika (to koření)" musí dát stejný základ bez ohledu na formulaci uvnitř.
        public static (string Base, bool HasParenthetical) SplitBaseAndParenthetical(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return (string.Empty, false);

            int idx = rawName.IndexOf('(');
            if (idx < 0) return (NormalizeForComparison(rawName), false);

            string basePart = rawName[..idx].Trim();
            return (NormalizeForComparison(basePart), true);
        }
    }
}