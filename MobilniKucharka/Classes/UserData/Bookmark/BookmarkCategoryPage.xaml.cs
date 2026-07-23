using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Services;
using System.Collections.ObjectModel;

// POZOR! Musí to být přesně stejné jako v XAML v parametru x:Class!
namespace MobilniKucharka.Classes.UserData.Bookmark;

public partial class BookmarkCategoryPage : ContentPage
{
    private readonly string _categoryName;

    public ObservableCollection<RecipeWithCost> Recipes { get; set; } = [];

    public BookmarkCategoryPage(string categoryName)
    {
        InitializeComponent();
        _categoryName = categoryName;
        Title = _categoryName;

        RecipesCollectionView.ItemsSource = Recipes;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRecipesSafeAsync();
    }

    private async Task LoadRecipesSafeAsync()
    {
        try
        {
            var rawRecipes = await App.Database.GetRecipesByCategoryAsync(_categoryName);

            int peopleCount = Preferences.Default.Get("PeopleCount", 2);
            double maxDailyBudget = Preferences.Default.Get("WeeklyBudget", 2000.0) / 7.0;

            Recipes.Clear();

            foreach (var r in rawRecipes)
            {
                double cost = await App.Database.CalculateRecipeCostAsync(r.Id, peopleCount);

                Recipes.Add(new RecipeWithCost
                {
                    Recipe = r,
                    CalculatedCost = cost,
                    IsWithinBudget = cost <= maxDailyBudget
                });
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Chyba načítání", $"Recepty se nepodařilo načíst.\nDetail: {ex.Message}", "OK");
            });
        }
    }

    private async void OnRecipeTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is RecipeWithCost selectedRecipe)
        {
            if (selectedRecipe.Recipe.IsDraft)
            {
                await Navigation.PushAsync(new CreateRecipePage(selectedRecipe.Recipe.Id));
            }
            else
            {
                await Navigation.PushAsync(new RecipeDetailPage(selectedRecipe));
            }
        }
    }
}