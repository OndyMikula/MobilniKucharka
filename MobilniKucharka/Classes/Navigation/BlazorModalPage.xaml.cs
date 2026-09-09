namespace MobilniKucharka.Classes.Navigation
{
    // Samostatná, jednorázová instance BlazorWebView pro obrazovky dosažitelné jen z nativního
    // Settings (Nahlásit chybu, Nápady, čtyři Legal stránky) - dřív sdílely stejnou jedinou
    // celoživotní WebView jako hlavní záložky (Recepty/Hledat/Záložky), což způsobovalo dva
    // propojené problémy: animace odkrytí ukazovala starou route (Recepty) místo cílové stránky, a
    // tlačítko Zpět se vracelo do historie WebView (na Recepty), ne na SettingsPage, protože
    // SettingsPage jako nativní stránka v týhle historii nikdy nebyla. Samostatná WebView =
    // standardní native PushAsync/PopAsync bez zvláštních triků.
    public partial class BlazorModalPage : ContentPage
    {
        public BlazorModalPage(string initialRoute)
        {
            ModalNavigationState.PendingRoute = initialRoute;
            InitializeComponent();
        }
    }
}