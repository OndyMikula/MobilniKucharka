namespace MobilniKucharka.Classes.Legal
{
    // Statický obsah právních dokumentů (ToS, ochrana osobních údajů, zdroje třetích stran).
    // Záměrně jen v angličtině, bez ohledu na aktuální jazyk appky (AppLanguageCode) - právní text
    // se nepřekládá strojově přes UiTranslator.Tr()/DeepL, aby nedošlo k nepřesnostem v právně
    // závazném znění. Tlačítka/odkaz v SettingsPage, které na tyhle stránky odkazují, zůstávají
    // přeložené normálně (Tr()) - jde jen o menu položky, ne o samotný právní obsah.
    //
    // License (Apache 2.0) tady záměrně NENÍ - jde o skutečný, nezkrácený text licence, ne shrnutí
    // v tomhle formátu (nadpis + odstavec), takže má vlastní stránku (LicensePage), která ho načítá
    // přímo z bundlovaného Raw assetu (Resources/Raw/LICENSE_apache2.txt).
    public static class LegalContent
    {
        // Odkaz na LICENSE soubor v repozitáři - použito jako doplňkové externí tlačítko na LicensePage.
        public const string LicenseFileUrl = "https://github.com/OndyMikula/MobilniKucharka/blob/main/LICENSE";

        public static string GetTitle(LegalDocumentType type) => type switch
        {
            LegalDocumentType.TermsOfService => "Terms of Service",
            LegalDocumentType.PrivacyPolicy => "Privacy Policy",
            LegalDocumentType.ThirdPartyNotices => "Third-Party Sources & Licenses",
            _ => string.Empty
        };

        public static List<LegalSection> GetSections(LegalDocumentType type) => type switch
        {
            LegalDocumentType.TermsOfService => TermsOfService(),
            LegalDocumentType.PrivacyPolicy => PrivacyPolicy(),
            LegalDocumentType.ThirdPartyNotices => ThirdPartyNotices(),
            _ => []
        };

        private static List<LegalSection> TermsOfService() =>
        [
            new() { Heading = "About the app", Body = "Mobilní Kuchařka (\"Mobile Cookbook\") is a personal, non-commercial project built by a single developer (Ondřej Mikula). The app is currently distributed informally via GitHub Releases and is not available on Google Play. By using the app, you agree to these terms." },
            new() { Heading = "Provided \"as is\"", Body = "The app is provided without any warranty, \"as is\". In particular, grocery cost estimates (based on Czech Statistical Office open data, dataset CEN02) and nutrition estimates are approximate and may not match real prices or the exact nutritional content of specific products. Any value marked \"Estimate\" is always approximate only." },
            new() { Heading = "Recipes and third-party content", Body = "Recipes imported from external sources (TheMealDB, Spoonacular) and recipes shared by other users of the app come from third parties. The developer does not guarantee their accuracy, completeness, or that they don't infringe on anyone's rights. The user who created or shared a recipe is solely responsible for its content." },
            new() { Heading = "Availability and changes", Body = "As a one-person hobby project, the app may be changed, paused, or discontinued at any time, without prior notice. Features that depend on external APIs (DeepL, TheMealDB, Spoonacular, Nutritionix, Open Food Facts, the Czech Statistical Office) may stop working if those services change or shut down." },
            new() { Heading = "Limitation of liability", Body = "To the maximum extent permitted by law, the developer is not liable for any damages arising from use of the app, including (but not limited to) damages caused by inaccurate price or nutrition estimates." },
            new() { Heading = "Governing law", Body = "These terms are governed by the laws of the Czech Republic." },
            new() { Heading = "Contact", Body = "Questions about these terms can be sent to: devzoufaly@gmail.com" }
        ];

        private static List<LegalSection> PrivacyPolicy() =>
        [
            new() { Heading = "Overview", Body = "Mobilní Kuchařka has no server of its own and no account/login system. All your data (recipes, bookmarks, settings, photos) is stored ONLY locally on your device, in an SQLite database inside the app's own folder. The developer has no access to it." },
            new() { Heading = "Data sent to third parties", Body = "Some features send specific data to external services, always only what's needed for that feature:\n• Recipe search and import (TheMealDB, Spoonacular) - sends the search text/recipe name.\n• Nutrition lookup (Nutritionix) - sends the ingredient text.\n• Barcode food lookup (Open Food Facts) - sends the barcode.\n• Interface and recipe translation (DeepL) - sends the text being translated.\n• Sharing a recipe via link - the recipe content (including any photo) is temporarily uploaded to a public GitHub repository (for up to 24 hours, or until the link is opened), then deleted. While active, it's theoretically accessible to anyone with the link.\n• Update checks - contact the public GitHub API without sending any personal data.\nThese services process data under their own privacy policies, which the developer has no control over." },
            new() { Heading = "Data backups", Body = "The backup feature creates a ZIP file containing all your data and lets you save/share it however you choose (email, cloud storage...) - the app itself never uploads this file anywhere." },
            new() { Heading = "Tracking and ads", Body = "The app contains no ads, no tracking tools, and no user behavior analytics." },
            new() { Heading = "Children", Body = "The app isn't specifically targeted at children, but it doesn't contain inappropriate content. Parents should supervise use by minors, particularly regarding the recipe link-sharing feature." },
            new() { Heading = "Deleting your data", Body = "You can delete all local data by uninstalling the app, or by deleting individual recipes/bookmarks directly within the app." },
            new() { Heading = "Contact", Body = "Questions about data handling: devzoufaly@gmail.com" }
        ];

        private static List<LegalSection> ThirdPartyNotices() =>
        [
            new() { Heading = "Czech Statistical Office (ČSÚ)", Body = "Average grocery prices are based on the CEN02 open dataset published by the Czech Statistical Office under an open-data license. The app is not an official ČSÚ product." },
            new() { Heading = "TheMealDB", Body = "Recipes and recipe images come from the public TheMealDB API (themealdb.com). The app is not affiliated with TheMealDB." },
            new() { Heading = "Spoonacular", Body = "Recipes, nutrition data, and preparation times come from the Spoonacular API (spoonacular.com), used in accordance with their developer terms." },
            new() { Heading = "Nutritionix", Body = "Natural-language ingredient parsing for nutrition estimates uses the Nutritionix API (nutritionix.com)." },
            new() { Heading = "Open Food Facts", Body = "Barcode-based food lookup uses the open Open Food Facts database (openfoodfacts.org), licensed under the Open Database License (ODbL)." },
            new() { Heading = "DeepL", Body = "Interface and recipe content translation is provided by the DeepL SE API (deepl.com)." },
            new() { Heading = "GitHub", Body = "Update checks and temporary recipe link-sharing use the public GitHub API and GitHub repositories (github.com), operated by GitHub, Inc." },
            new() { Heading = "Notice", Body = "The app is not officially affiliated with, sponsored by, or endorsed by any of the companies or organizations listed above. All trademarks belong to their respective owners." }
        ];
    }
}