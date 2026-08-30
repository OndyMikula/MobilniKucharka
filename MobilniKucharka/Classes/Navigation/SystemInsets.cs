namespace MobilniKucharka.Classes.Navigation
{
    // Sdílené, platformově neutrální úložiště pro aktuální výšku systémové navigační lišty (viz
    // MainActivity.SetupSystemInsetsListener na Androidu) - Blazor komponenty (MainLayout.razor)
    // ho můžou odebírat bez #if ANDROID uvnitř .razor souboru. Na jiných platformách než Android
    // zůstává BottomDp navždy 0 a event se nikdy nevyvolá - MainLayout pak jen nedostane žádnou
    // aktualizaci a CSS proměnná zůstane na výchozí 0px.
    public static class SystemInsets
    {
        public static double BottomDp { get; private set; }
        public static event Action? BottomChanged;

        public static void SetBottom(double dp)
        {
            if (BottomDp == dp) return;
            BottomDp = dp;
            BottomChanged?.Invoke();
        }
    }
}