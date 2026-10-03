<details>
  <summary>EN</summary>
 

 # Mobile Cookbook

An app for meal planning, recipe management, and estimating grocery costs—developed as a personal project.

## What the app can do

- Plans meals based on your budget and the number of people in your household
- First-launch setup walks you through choosing a language, optionally restoring a prior backup, then your household size, diet, and kitchen equipment — language switches immediately, with no restart needed
- Filters recipes by dietary restrictions (vegetarian, vegan, lactose-free) and available kitchen equipment, checked automatically against each recipe's actual ingredients and steps
- Searches your own recipes by name, with filters for diet and kitchen equipment
- Discover new recipes from the internet based on dietary preferences ("Surprise me!")
- Internet search stays reliable even under heavy app-wide usage — results are briefly cached on your device, and the app automatically relies more on its unlimited recipe source if a third-party source's daily limit is ever reached, so you still get results instead of an error
- Create and edit your own recipes with automatic, real-time saving (an abandoned, empty draft cleans itself up automatically)
- A dedicated ingredient management screen to rename, merge, delete, or manually price any ingredient across your whole recipe collection
- Estimate nutritional values (protein, carbohydrates, fats, sugars), including a clear indication when a value is an estimate
- Estimated grocery cost based on average prices from the Czech Statistical Office, with the option to manually adjust the price of any ingredient
- Linking different names for the same ingredient to a single price (aliases), with automatic duplicate detection and cleanup across languages, spelling, and capitalization
- Bookmarks for organizing recipes, including pinning, manual sorting, an optional description, and a custom folder image
- Recipe ratings, settable by tapping a star or dragging a slider
- Backup and restore of all your data across devices — recipes, bookmarks, photos, and your app preferences (household size, budget, diet/equipment filters, language, and theme)
- Share individual recipes as a portable file or a shareable link
- Automatic translation of recipes, kitchen equipment tags, and the interface between Czech and English
- Light/dark theme and Czech/English language support
- Check for new app versions directly within the app
- Send a bug report or suggest a new feature straight to the developer from inside the app, with an optional way for the developer to reply to you by email

## How the app works (for developers)

**Technology:** .NET MAUI Blazor Hybrid (C#), targeting the Android platform (`net10.0-android`). The entire interface is built with Razor components and CSS, running inside an embedded WebView. All data is stored locally in an SQLite database on the user’s device—the app does not have its own backend server.

**Data layer:** `BudgetPlannerService` is a central service that wraps the SQLite database—it manages recipes, ingredients, prices, bookmarks, and their relationships. Ingredient prices are calculated from two sources: a price manually entered by the user (which always takes precedence) or the average price from open data provided by the Czech Statistical Office (CEN02 dataset). A dedicated in-app screen lets you rename, merge, delete, or reprice any ingredient directly, on top of the automatic cleanup described next.

**External APIs:**
- **TheMealDB** — recipe search by diet
- **Spoonacular** — an additional source of recipes, including actual nutritional data, preparation time, and structured per-step kitchen-equipment data
- **Nutritionix** — calculates nutritional values from ingredient text; if this fails or the key is not configured, the app estimates values from its own local table of common ingredients
- **Open Food Facts** — search for foods by barcode
- **DeepL** — translates recipe content and the app's own interface between Czech and English, with local caching so the same text is never sent for translation twice. The same caching principle extends to the app's shared ingredient catalog: ingredient names are automatically deduplicated and kept correctly bilingual across languages, spelling variants, and capitalization, with a manual management screen available as a fallback for cases automatic matching can't safely resolve on its own.

Recipes imported from external sources are saved to the local database the first time they are viewed so they can be bookmarked and rated just like custom recipes. Any user-supplied photo is automatically resized before being stored, so it always displays correctly regardless of how large the original camera photo was.

**Recipe sharing:** individual recipes can be exported as a plain, human-readable `.json` file (no special app needed to open it) or shared via a temporary link backed by a separate GitHub repository, which also generates real Android App Links so opening a shared link on another device jumps straight into the app.

**Feedback:** bug reports and feature ideas can be sent directly from within the app, with no need to leave it. Since the app has no backend server, sending works by triggering a small, isolated GitHub Actions workflow (hosted in its own, otherwise-empty repository, kept deliberately separate from the app's real source code) that relays the message to the developer's email — including an optional reply-to address if the user chooses to provide one.

Set **API keys** in `Services/Secrets.txt` and rename the file to `Services/Secrets.cs`.

**Releasing versions:** GitHub Actions (`.github/workflows/release.yml`) automatically reads the version from `.csproj` when you push to `main`, and if it’s a new version, it creates a tag, generates a changelog from the commits, and attaches a signed APK as a GitHub Release. The app itself checks for the availability of a new version via the GitHub API — with the exception of installations from Google Play, where updates are handled exclusively by Play itself.
</details>


<details>
<summary>CZ</summary>

# Mobilní Kuchařka

Aplikace pro plánování jídelníčku, správu receptů a odhad nákladů na nákup — postavená jako osobní projekt.

## Co aplikace umí

- Plánuje jídelníček podle rozpočtu a počtu lidí v domácnosti
- Při prvním spuštění tě provede výběrem jazyka, volitelnou obnovou dřívější zálohy a nastavením domácnosti, diety a spotřebičů — jazyk se přepne okamžitě, bez nutnosti restartu
- Filtruje recepty podle dietních omezení (vegetariánská, veganská, bezlaktózová dieta) a dostupného kuchyňského vybavení, automaticky ověřeno podle skutečných surovin a postupu každého receptu
- Vyhledávání vlastních receptů podle názvu, s filtrem podle diety a vybavení kuchyně
- Objevování nových receptů z internetu podle dietních preferencí ("Překvap mě!")
- Vyhledávání na internetu zůstává spolehlivé i při velkém vytížení aplikace — výsledky se krátce ukládají do mezipaměti na zařízení a appka se automaticky více spoléhá na neomezený zdroj receptů, pokud denní limit jiného zdroje dojde, takže vždy dostaneš výsledky místo chyby
- Vytváření a úprava vlastních receptů s automatickým průběžným ukládáním (opuštěný prázdný koncept se sám uklidí)
- Samostatná obrazovka pro správu surovin — přejmenování, sloučení, smazání nebo ruční nastavení ceny kterékoli suroviny napříč všemi recepty
- Odhad nutričních hodnot (bílkoviny, sacharidy, tuky, cukry), včetně jasného označení, kdy jde o odhad
- Odhad ceny nákupu z průměrných cen ČSÚ, s možností ruční úpravy ceny jakékoli suroviny
- Propojování různých názvů stejné suroviny na jednu cenu (aliasy), včetně automatické detekce a úklidu duplicit napříč jazyky, pravopisem a velikostí písmen
- Záložky pro organizaci receptů, včetně připínání, ručního řazení, volitelného popisu a vlastního obrázku složky
- Hodnocení receptů — klepnutím na hvězdičku nebo tažením posuvníku
- Záloha a obnova všech tvých dat mezi zařízeními — recepty, záložky, fotky i nastavení aplikace (počet lidí, rozpočet, dietní/spotřebičové filtry, jazyk a motiv)
- Sdílení jednotlivých receptů jako přenositelný soubor nebo odkaz
- Automatický překlad receptů, štítků kuchyňského vybavení i samotného rozhraní mezi češtinou a angličtinou
- Světlý/tmavý motiv a čeština/angličtina
- Kontrola nových verzí aplikace přímo v appce
- Nahlášení chyby nebo návrh nového nápadu přímo vývojáři z aplikace, s možností nechat mu i svůj e-mail pro odpověď

## Jak aplikace funguje (pro vývojáře)

**Technologie:** .NET MAUI Blazor Hybrid (C#), cílená platforma Android (`net10.0-android`). Celé rozhraní je postavené na Razor komponentách a CSS běžících ve vnořeném WebView. Veškerá data se ukládají lokálně v SQLite databázi na zařízení uživatele — aplikace nemá žádný vlastní backend server.

**Datová vrstva:** `BudgetPlannerService` je centrální služba obalující SQLite databázi — spravuje recepty, suroviny, ceny, záložky a jejich propojení. Ceny surovin se počítají ze dvou zdrojů: uživatelem ručně zadaná cena (má vždy přednost) nebo průměrná cena z otevřených dat ČSÚ (dataset CEN02). Samostatná obrazovka v appce navíc umožňuje kteroukoli surovinu přímo přejmenovat, sloučit, smazat nebo přecenit, nad rámec automatického úklidu popsaného níže.

**Externí API:**
- **TheMealDB** — vyhledávání receptů podle diety
- **Spoonacular** — doplňkový zdroj receptů, včetně reálných nutričních dat, doby přípravy a strukturovaných dat o potřebném vybavení u jednotlivých kroků
- **Nutritionix** — dopočet nutričních hodnot z textu ingrediencí; pokud selže nebo není nakonfigurován klíč, aplikace odhadne hodnoty z vlastní lokální tabulky běžných surovin
- **Open Food Facts** — vyhledávání potravin podle čárového kódu
- **DeepL** — překládá obsah receptů i samotné rozhraní aplikace mezi češtinou a angličtinou, s lokální cache, aby se stejný text nikdy neposílal na překlad dvakrát. Stejný princip cache platí i pro sdílený katalog surovin v appce — názvy surovin se automaticky slučují a udržují správně dvojjazyčné napříč jazyky, pravopisnými variantami i velikostí písmen, s ruční obrazovkou pro správu jako záložní možností pro případy, které automatika bezpečně sama nevyřeší.

Recepty importované z externích zdrojů se ukládají do lokální databáze při prvním zobrazení, aby šly bookmarkovat a hodnotit stejně jako vlastní recepty. Jakákoli fotka nahraná uživatelem se před uložením automaticky zmenší, aby se vždy správně zobrazila bez ohledu na to, jak velká byla původní fotka z fotoaparátu.

**Sdílení receptů:** jednotlivé recepty lze exportovat jako obyčejný, čitelný soubor `.json` (nepotřebuje žádnou speciální appku k otevření) nebo sdílet přes dočasný odkaz uložený v samostatném GitHub repozitáři, který zároveň generuje skutečné Android App Links, takže otevření sdíleného odkazu na jiném zařízení rovnou skočí do aplikace.

**Zpětná vazba:** hlášení chyb a nápady na vylepšení lze poslat přímo z aplikace, bez nutnosti ji opouštět. Protože aplikace nemá vlastní backend server, odeslání funguje spuštěním malého, izolovaného GitHub Actions workflow (uloženého ve vlastním, jinak prázdném repozitáři, záměrně odděleném od skutečného zdrojového kódu aplikace), který zprávu přepošle na e-mail vývojáře — včetně volitelné adresy pro odpověď, pokud ji uživatel zadá.

**API klíče** nastavte v `Services/Secrets.txt` a přejmenujte na `Services/Secrets.cs`.

**Vydávání verzí:** GitHub Actions (`.github/workflows/release.yml`) při pushi do `main` automaticky přečte verzi z `.csproj`, a pokud jde o novou verzi, vytvoří tag, vygeneruje changelog z commitů a přiloží podepsaný APK jako GitHub Release. Aplikace sama kontroluje dostupnost nové verze přes GitHub API — s výjimkou instalací z Google Play, kde aktualizace řeší výhradně Play samotný.

</details>