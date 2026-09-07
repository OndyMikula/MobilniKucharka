using System.Globalization;
using System.Text.Json;

namespace MobilniKucharka.Services
{
    // Zálohuje/obnovuje uživatelské preference (Preferences.Default), které DataBackupService
    // samo o sobě nezahrnuje - ten zálohuje jen FileSystem.AppDataDirectory (SQLite, fotky), zatímco
    // Preferences žijí v samostatném úložišti (na Androidu SharedPreferences). Bez tohohle by obnova
    // ze zálohy vrátila recepty/záložky/fotky správně, ale počet lidí, rozpočet, diety, jazyk a
    // motiv by po obnově spadly zpět na kódem dané výchozí hodnoty.
    public static class PreferencesBackupService
    {
        public const string FileNameInZip = "preferences_backup.json";

        private class Model
        {
            public int? PeopleCount { get; set; }
            public double? WeeklyBudget { get; set; }
            public string? UserDiets { get; set; }
            public string? UserAppliances { get; set; }
            public string? AppLanguageCode { get; set; }
            public string? AppLanguageName { get; set; }
            public string? AppTheme { get; set; }
        }

        public static string CaptureAsJson()
        {
            var model = new Model
            {
                PeopleCount = Preferences.Default.Get("PeopleCount", 2),
                WeeklyBudget = Preferences.Default.Get("WeeklyBudget", 2000.0),
                UserDiets = Preferences.Default.Get("UserDiets", string.Empty),
                UserAppliances = Preferences.Default.Get("UserAppliances", string.Empty),
                AppLanguageCode = Preferences.Default.Get("AppLanguageCode", "cs"),
                AppLanguageName = Preferences.Default.Get("AppLanguageName", "Čeština"),
                AppTheme = Preferences.Default.Get("AppTheme", "Podle systému")
            };

            return JsonSerializer.Serialize(model);
        }

        // Každé pole se zapíše, jen pokud v záloze skutečně je - starší záloha vytvořená appkou
        // ještě bez téhle funkce prostě nebude preference_backup.json vůbec obsahovat, a tahle
        // metoda se v tom případě nezavolá vůbec (viz DataBackupService.ImportAsync).
        public static void ApplyFromJson(string json)
        {
            Model? model;
            try
            {
                model = JsonSerializer.Deserialize<Model>(json);
            }
            catch (JsonException)
            {
                return;
            }
            if (model == null) return;

            if (model.PeopleCount.HasValue) Preferences.Default.Set("PeopleCount", model.PeopleCount.Value);
            if (model.WeeklyBudget.HasValue) Preferences.Default.Set("WeeklyBudget", model.WeeklyBudget.Value);
            if (model.UserDiets != null) Preferences.Default.Set("UserDiets", model.UserDiets);
            if (model.UserAppliances != null) Preferences.Default.Set("UserAppliances", model.UserAppliances);
            if (!string.IsNullOrWhiteSpace(model.AppLanguageCode)) Preferences.Default.Set("AppLanguageCode", model.AppLanguageCode);
            if (!string.IsNullOrWhiteSpace(model.AppLanguageName)) Preferences.Default.Set("AppLanguageName", model.AppLanguageName);
            if (!string.IsNullOrWhiteSpace(model.AppTheme)) Preferences.Default.Set("AppTheme", model.AppTheme);
        }

        // Zapsání do Preferences samo o sobě motiv/jazyk viditelně nezmění - obojí se jinde v appce
        // aplikuje jen jako vedlejší efekt ruční akce uživatele (SettingsPage.OnThemeChanged/
        // OnLanguageChanged). Po obnově zálohy žádná taková ruční akce neproběhne, takže tohle je
        // potřeba zavolat výslovně, ať se restart motivu/jazyka projeví hned, ne až při příští
        // návštěvě Nastavení.
        public static void ApplyRuntimeSideEffects()
        {
            string languageCode = Preferences.Default.Get("AppLanguageCode", "cs");
            var culture = new CultureInfo(languageCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            string theme = Preferences.Default.Get("AppTheme", "Podle systému");
            Application.Current?.UserAppTheme = theme switch
                {
                    "Světlý" => AppTheme.Light,
                    "Tmavý" => AppTheme.Dark,
                    _ => AppTheme.Unspecified
                };
        }

        public static bool IsPreferencesEntry(string entryName) =>
            string.Equals(entryName, FileNameInZip, StringComparison.OrdinalIgnoreCase);
    }
}