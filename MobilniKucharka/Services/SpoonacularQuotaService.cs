namespace MobilniKucharka.Services
{
    // Sleduje denní kvótu Spoonacular podle hlaviček, které API vrací v KAŽDÉ odpovědi
    // (X-API-Quota-Left) - žádné volání navíc není potřeba. Appka sama přestane Spoonacular volat,
    // než narazí na skutečnou chybu kvóty - uživatel nikdy neuvidí chybu, jen míň výsledků,
    // doplněných pořád z neomezeného MealDB.
    public static class SpoonacularQuotaService
    {
        private const double SearchReserveBuffer = 5.0; // rezerva pro detail receptu (stojí víc bodů než hledání)

        public static void UpdateFromResponseHeaders(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("X-API-Quota-Left", out var values) &&
                double.TryParse(values.FirstOrDefault(), System.Globalization.CultureInfo.InvariantCulture, out var left))
            {
                Preferences.Default.Set("SpoonacularQuotaLeft", left);
                Preferences.Default.Set("SpoonacularQuotaDay", DateTime.UtcNow.Date.ToString("O"));
            }
        }

        public static bool HasSearchQuota()
        {
            string? savedDay = Preferences.Default.Get("SpoonacularQuotaDay", (string?)null);
            if (savedDay != DateTime.UtcNow.Date.ToString("O"))
                return true; // nový den (nebo první spuštění) - appka kvótu ještě neměřila

            double left = Preferences.Default.Get("SpoonacularQuotaLeft", 100.0);
            return left > SearchReserveBuffer;
        }
    }
}