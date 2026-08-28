
<details>
  <summary>EN</summary>
 

 # Mobile Cookbook

An app for meal planning, recipe management, and estimating grocery costs—developed as a personal project.

## What the app can do

- Plans meals based on your budget and the number of people in your household
- Searches your own recipes by name, with filters for diet and kitchen equipment
- Discover new recipes from the internet based on dietary preferences (“Surprise me!”)
- Create and edit your own recipes with automatic, real-time saving
- Estimate nutritional values (protein, carbohydrates, fats, sugars), including a clear indication when a value is an estimate
- Estimated grocery cost based on average prices from the Czech Statistical Office, with the option to manually adjust the price of any ingredient
- Linking different names for the same ingredient to a single price (aliases)
- Bookmarks for organizing recipes, including pinning and manual sorting
- Recipe ratings
- Backup and restore of all data (recipes, bookmarks, photos, settings) across devices
- Share individual recipes as a portable file or a shareable link
- Automatic translation of recipes and the interface between Czech and English
- Light/dark theme and Czech/English language support
- Check for new app versions directly within the app

## How the app works (for developers)

**Technology:** .NET MAUI (C#), targeting the Android platform (`net10.0-android`). All data is stored locally in an SQLite database on the user’s device—the app does not have its own backend server.

**Data layer:** `BudgetPlannerService` is a central service that wraps the SQLite database—it manages recipes, ingredients, prices, bookmarks, and their relationships. Ingredient prices are calculated from two sources: a price manually entered by the user (which always takes precedence) or the average price from open data provided by the Czech Statistical Office (CEN02 dataset).

**External APIs:**
- **TheMealDB** — recipe search by diet
- **Spoonacular** — an additional source of recipes, including actual nutritional data and preparation time
- **Nutritionix** — calculates nutritional values from ingredient text; if this fails or the key is not configured, the app estimates values from its own local table of common ingredients (`NutritionEstimationService`)
- **Open Food Facts** — search for foods by barcode
- **DeepL** — translates recipe content and the app's own interface between Czech and English, with local caching so the same text is never sent for translation twice

Recipes imported from external sources are saved to the local database the first time they are viewed so they can be bookmarked and rated just like custom recipes.

**Recipe sharing:** individual recipes can be exported as a plain, human-readable `.json` file (no special app needed to open it) or shared via a temporary link backed by a separate GitHub repository, which also generates real Android App Links so opening a shared link on another device jumps straight into the app.

Set **API keys** in `Services/Secrets.txt` and rename the file to `Services/Secrets.cs`.

**Releasing versions:** GitHub Actions (`.github/workflows/release.yml`) automatically reads the version from `.csproj` when you push to `main`, and if it’s a new version, it creates a tag, generates a changelog from the commits, and attaches a signed APK as a GitHub Release. The app itself checks for the availability of a new version via the GitHub API (`UpdateCheckService`) — with the exception of installations from Google Play, where updates are handled exclusively by Play itself.
</details>


<details>
<summary>CZ</summary>

# Mobilní Kuchařka

Aplikace pro plánování jídelníčku, správu receptů a odhad nákladů na nákup — postavená jako osobní projekt.

## Co aplikace umí

- Plánuje jídelníček podle rozpočtu a počtu lidí v domácnosti
- Vyhledávání vlastních receptů podle názvu, s filtrem podle diety a vybavení kuchyně
- Objevování nových receptů z internetu podle dietních preferencí ("Překvap mě!")
- Vytváření a úprava vlastních receptů s automatickým průběžným ukládáním
- Odhad nutričních hodnot (bílkoviny, sacharidy, tuky, cukry), včetně jasného označení, kdy jde o odhad
- Odhad ceny nákupu z průměrných cen ČSÚ, s možností ruční úpravy ceny jakékoli suroviny
- Propojování různých názvů stejné suroviny na jednu cenu (aliasy)
- Záložky pro organizaci receptů, včetně připínání a ručního řazení
- Hodnocení receptů
- Záloha a obnova všech dat (recepty, záložky, fotky, nastavení) mezi zařízeními
- Sdílení jednotlivých receptů jako přenositelný soubor nebo odkaz
- Automatický překlad receptů i rozhraní mezi češtinou a angličtinou
- Světlý/tmavý motiv a čeština/angličtina
- Kontrola nových verzí aplikace přímo v appce

## Jak aplikace funguje (pro vývojáře)

**Technologie:** .NET MAUI (C#), cílená platforma Android (`net10.0-android`). Veškerá data se ukládají lokálně v SQLite databázi na zařízení uživatele — aplikace nemá žádný vlastní backend server.

**Datová vrstva:** `BudgetPlannerService` je centrální služba obalující SQLite databázi — spravuje recepty, suroviny, ceny, záložky a jejich propojení. Ceny surovin se počítají ze dvou zdrojů: uživatelem ručně zadaná cena (má vždy přednost) nebo průměrná cena z otevřených dat ČSÚ (dataset CEN02).

**Externí API:**
- **TheMealDB** — vyhledávání receptů podle diety
- **Spoonacular** — doplňkový zdroj receptů, včetně reálných nutričních dat a doby přípravy
- **Nutritionix** — dopočet nutričních hodnot z textu ingrediencí; pokud selže nebo není nakonfigurován klíč, aplikace odhadne hodnoty z vlastní lokální tabulky běžných surovin (`NutritionEstimationService`)
- **Open Food Facts** — vyhledávání potravin podle čárového kódu
- **DeepL** — překládá obsah receptů i samotné rozhraní aplikace mezi češtinou a angličtinou, s lokální cache, aby se stejný text nikdy neposílal na překlad dvakrát

Recepty importované z externích zdrojů se ukládají do lokální databáze při prvním zobrazení, aby šly bookmarkovat a hodnotit stejně jako vlastní recepty.

**Sdílení receptů:** jednotlivé recepty lze exportovat jako obyčejný, čitelný soubor `.json` (nepotřebuje žádnou speciální appku k otevření) nebo sdílet přes dočasný odkaz uložený v samostatném GitHub repozitáři, který zároveň generuje skutečné Android App Links, takže otevření sdíleného odkazu na jiném zařízení rovnou skočí do appky.

**API klíče** nastavte v `Services/Secrets.txt` a přejmenujte na `Services/Secrets.cs`.

**Vydávání verzí:** GitHub Actions (`.github/workflows/release.yml`) při pushi do `main` automaticky přečte verzi z `.csproj`, a pokud jde o novou verzi, vytvoří tag, vygeneruje changelog z commitů a přiloží podepsaný APK jako GitHub Release. Aplikace sama kontroluje dostupnost nové verze přes GitHub API (`UpdateCheckService`) — s výjimkou instalací z Google Play, kde aktualizace řeší výhradně Play samotný.

</details>