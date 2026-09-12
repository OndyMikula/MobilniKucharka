namespace MobilniKucharka.Classes.Navigation
{
    public static class SystemInsets
    {
        public static double BottomDp { get; private set; }
        public static double TopDp { get; private set; }
        public static event Action? BottomChanged;
        public static event Action? TopChanged;

        public static void SetBottom(double dp)
        {
            if (BottomDp == dp) return;
            BottomDp = dp;
            BottomChanged?.Invoke();
        }

        // Výška status baru - Blazor stránky bez nativní title bar (Shell.NavBarIsVisible="False",
        // viz BlazorShellPage/BlazorModalPage) se kreslí edge-to-edge i POD status barem, ne jen
        // nad systémovou navigační lištou. Nativní stránky (SettingsPage) mají vlastní title bar,
        // který si prostor pod status barem hlídá sám - proto jen Blazor stránky tenhle inset
        // potřebují aplikovat ručně.
        public static void SetTop(double dp)
        {
            if (TopDp == dp) return;
            TopDp = dp;
            TopChanged?.Invoke();
        }
    }
}