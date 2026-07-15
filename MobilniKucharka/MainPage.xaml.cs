using MobilniKucharka.Classes;
using MobilniKucharka.Services;

namespace MobilniKucharka
{
    public partial class MainPage : ContentPage
    {
        private readonly BudgetPlannerService _budgetService;

        public MainPage()
        {
            InitializeComponent();

            // Nastavení cesty k databázi (v MAUI se používá lokální složka aplikace)
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "kucharka.db3");
            _budgetService = new BudgetPlannerService(dbPath);
        }

        // Zavolá se pokaždé, když se stránka zobrazí na displeji
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadRecipesDataAsync();
            UpdateSummaryUI();
        }

        private async Task LoadRecipesDataAsync()
        {
            // Zde voláme náš složitý výpočetní mozek, který běží na pozadí
            var recipes = await _budgetService.GetPlanAsync();

            // Výsledek vložíme do CollectionView
            RecipesCollectionView.ItemsSource = recipes;
        }

        private void UpdateSummaryUI()
        {
            // Načtení aktuálních preferencí pro hlavičku
            int people = Preferences.Default.Get("PeopleCount", 2);
            double budget = Preferences.Default.Get("WeeklyBudget", 2000.0);
            string shop = Preferences.Default.Get("ShopColName", "PriceLidl").Replace("Price", ""); // Z "PriceLidl" udělá "Lidl"

            SettingsSummaryLabel.Text = $"{people} osoby | Rozpočet: {budget} Kč/týden | Obchod: {shop}";
        }

        private async void OnEditSettingsClicked(object sender, EventArgs e)
        {
            // Otevře znovu OnboardingPage pro úpravu preferencí
            await Navigation.PushAsync(new OnboardingPage());
        }

        private async void OnRecipeTapped(object sender, TappedEventArgs e)
        {
            // ZMĚNA ZDE: Kontrolujeme, zda je sender Border, nikoliv Frame
            if (sender is Border border && border.BindingContext is RecipeWithCost selectedItem)
            {
                RecipesCollectionView.SelectedItem = null;
                await Navigation.PushAsync(new RecipeDetailPage(selectedItem));
            }
        }
    }
}