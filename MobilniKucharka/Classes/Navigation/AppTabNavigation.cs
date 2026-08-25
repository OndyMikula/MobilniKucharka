using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Classes.UserData.Bookmark;

namespace MobilniKucharka.Classes.Navigation
{
    // Sdílená navigace mezi 3 hlavními "hub" stránkami (Recepty/Hledat/Záložky) volaná z
    // BottomNavBar.TabRequested. Vždy se nejdřív vrátí na root (MainPage) přes PopToRootAsync,
    // ne postupným Push/Push/Push - takže zásobník stránek zůstává nejvýš 2 úrovně hluboký
    // (root + max. jedna hub stránka) bez ohledu na to, kolikrát uživatel mezi záložkami přepne.
    public static class AppTabNavigation
    {
        public static async Task GoToTabAsync(INavigation navigation, AppTab targetTab)
        {
            switch (targetTab)
            {
                case AppTab.Recipes:
                    await navigation.PopToRootAsync();
                    break;

                case AppTab.Search:
                    await navigation.PopToRootAsync(false);
                    await navigation.PushAsync(new SearchPage());
                    break;

                case AppTab.Bookmarks:
                    await navigation.PopToRootAsync(false);
                    await navigation.PushAsync(new BookmarksPage());
                    break;
            }
        }
    }
}