using Microsoft.Maui.Controls;
using MobilniKucharka.Classes;
using MobilniKucharka.Services;

namespace MobilniKucharka
{
    public partial class RecipeDetailPage : ContentPage
    {
        private readonly RecipeWithCost _recipeWithCost;
        private readonly BudgetPlannerService _budgetService;

        public RecipeDetailPage(RecipeWithCost selectedRecipe)
        {
            InitializeComponent();
            _recipeWithCost = selectedRecipe;

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "kucharka.db3");
            _budgetService = new BudgetPlannerService(dbPath);

            LoadRecipeDetails();
        }

        private async void LoadRecipeDetails()
        {
            var recipe = _recipeWithCost.Recipe;

            // 1. Základní info o receptu
            RecipeImage.Source = recipe.ImageUrl;
            RecipeNameLabel.Text = Preferences.Default.Get("AppLanguageCode", "cs") == "en" ? recipe.Name_EN : recipe.Name_CS;

            // 2. Nutriční hodnoty
            ProteinLabel.Text = $"{recipe.Protein}g";
            CarbsLabel.Text = $"{recipe.Carbs}g";
            FatLabel.Text = $"{recipe.Fat}g";
            SugarLabel.Text = $"{recipe.Sugar}g";

            // 3. Načtení surovin přepočítaných na lidi a obchod
            int peopleCount = Preferences.Default.Get("PeopleCount", 2);
            string shopColumn = Preferences.Default.Get("ShopColName", "PriceLidl");
            string shopName = shopColumn.Replace("Price", "");

            PeopleCountBadge.Text = $"(pro {peopleCount} {(peopleCount == 1 ? "osobu" : (peopleCount < 5 ? "osoby" : "lidí"))} v {shopName})";

            var ingredients = await _budgetService.GetIngredientsForRecipeAsync(recipe.Id, peopleCount, shopColumn);
            BindableLayout.SetItemsSource(IngredientsLayout, ingredients);

            // Zobrazení celkové ceny nákupu
            TotalPriceLabel.Text = $"Celkem za nákup: {_recipeWithCost.CalculatedCost:N0} Kč";

            // 4. Postup přípravy (Číslované kroky)
            // Zjistíme správný jazyk pro postupy
            string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
            var rawSteps = currentLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;

            // Převedeme čisté stringy na objekty s indexem (číslem kroku)
            var structuredSteps = rawSteps.Select((stepText, index) => new DisplayStep
            {
                StepNumber = index + 1,
                StepText = stepText
            }).ToList();

            BindableLayout.SetItemsSource(StepsLayout, structuredSteps);
        }
    }

    // Pomocná třída pro očíslování kroků v XAML šabloně
    public class DisplayStep
    {
        public int StepNumber { get; set; }
        public string StepText { get; set; } = string.Empty;
    }
}