namespace MobilniKucharka.Classes.Navigation
{
    public partial class BottomNavBar : ContentView
    {
        private const uint AnimationDurationMs = 220;

        private AppTab _activeTab = AppTab.Recipes;
        private double _columnWidth;
        private bool _hasLaidOutOnce;

        // Vyvoláno, když uživatel klepne na jinou záložku, než na které právě je - hostující
        // stránka (MainPage/SearchPage/BookmarksPage) na tohle naváže vlastní navigací, protože
        // ContentView nemá vlastní přístup k INavigation (viz AppTabNavigation).
        public event EventHandler<AppTab>? TabRequested;

        public BottomNavBar()
        {
            InitializeComponent();
        }

        // Zavolat z konstruktoru/OnAppearing hostující stránky - nastaví, která záložka je aktivní.
        // Dokud layout ještě neproběhl (_columnWidth == 0), pozice se jen zapamatuje a doplní ji
        // OnRootGridSizeChanged bez animace; jinak, pokud se aktivní záložka opravdu mění, se modrá
        // "pilulka" plynule přesune (viz AnimateToActiveTab).
        public void SetActiveTab(AppTab tab)
        {
            bool isChange = tab != _activeTab || !_hasLaidOutOnce;
            _activeTab = tab;
            UpdateButtonTextColors();

            if (_columnWidth <= 0) return;

            if (isChange)
                _ = AnimateToActiveTab();
        }

        private void OnRootGridSizeChanged(object sender, EventArgs e)
        {
            if (RootGrid.Width <= 0) return;

            double contentWidth = RootGrid.Width - RootGrid.Padding.Left - RootGrid.Padding.Right;
            _columnWidth = contentWidth / 3.0;
            IndicatorPill.WidthRequest = Math.Max(_columnWidth - 10, 10);

            // Bez animace - jde buď o úplně první layout, nebo o změnu orientace/velikosti obrazovky,
            // ne o skutečné přepnutí záložky uživatelem (to řeší SetActiveTab/AnimateToActiveTab).
            IndicatorPill.TranslationX = _columnWidth * (int)_activeTab;
            _hasLaidOutOnce = true;
        }

        private async Task AnimateToActiveTab()
        {
            double targetX = _columnWidth * (int)_activeTab;
            await IndicatorPill.TranslateTo(targetX, 0, AnimationDurationMs, Easing.CubicInOut);
        }

        private void UpdateButtonTextColors()
        {
            SetButtonActiveState(RecipesButton, _activeTab == AppTab.Recipes);
            SetButtonActiveState(SearchButton, _activeTab == AppTab.Search);
            SetButtonActiveState(BookmarksButton, _activeTab == AppTab.Bookmarks);
        }

        // Aktivní tlačítko dostane bílý text (stojí na modré pilulce). Neaktivní se vrátí ke svému
        // původnímu AppThemeBinding z XAML přes ClearValue (ne natvrdo dosazená barva) - díky tomu
        // zůstává správně světlé/tmavé i po přepnutí motivu za běhu, bez ručního přepočítávání.
        private static void SetButtonActiveState(Button button, bool isActive)
        {
            if (isActive)
                button.TextColor = Colors.White;
            else
                button.ClearValue(Button.TextColorProperty);
        }

        private void OnRecipesTapped(object sender, EventArgs e) => RequestTab(AppTab.Recipes);
        private void OnSearchTapped(object sender, EventArgs e) => RequestTab(AppTab.Search);
        private void OnBookmarksTapped(object sender, EventArgs e) => RequestTab(AppTab.Bookmarks);

        private void RequestTab(AppTab tab)
        {
            if (tab == _activeTab) return; // klepnutí na už aktivní záložku - nic se neděje
            TabRequested?.Invoke(this, tab);
        }
    }
}