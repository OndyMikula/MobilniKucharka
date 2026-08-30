using MobilniKucharka.Classes.Recipe;
using MobilniKucharka.Services;
using System.Collections.ObjectModel;

// POZOR! Musí to být přesně stejné jako v XAML v parametru x:Class!
namespace MobilniKucharka.Classes.UserData.Bookmark;

public partial class BookmarkCategoryPage : ContentPage
{
    private readonly string _categoryName;

    public ObservableCollection<RecipeWithCost> Recipes { get; set; } = [];

    private static string Tr(string csText) => MobilniKucharka.Translation.UiTranslator.Tr(csText);

    public BookmarkCategoryPage(string categoryName)
    {
        InitializeComponent();
        _categoryName = categoryName;

        // Tr() zvládne i výchozí čtyři záložky rovnou - jejich přesné české názvy jsou zároveň
        // klíči v ui_translations_en.json ("Oblíbené", "Vytvořené recepty"...), takže žádný
        // samostatný překladový switch (jako TranslateCategoryName v BookmarksPage.razor) tu není
        // potřeba. U vlastních (nepřeložených) záložek Tr() beze změny vrátí originální název.
        Title = Tr(_categoryName);
        CategoryNameLabel.Text = Tr(_categoryName);

        RecipesCollectionView.ItemsSource = Recipes;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBookmarkHeaderAsync();
        await LoadRecipesSafeAsync();
    }

    // Popis záložky (Bookmark.Description) je volitelný - řádek se zobrazí jen když je vyplněný,
    // ať prázdné záložky nemají pod názvem zbytečnou mezeru. Načítá se znovu při každém OnAppearing,
    // aby se hned projevila případná úprava přes CreateBookmarkPage(categoryName) v editačním režimu.
    private async Task LoadBookmarkHeaderAsync()
    {
        var bookmark = await App.Database.GetBookmarkByNameAsync(_categoryName);
        if (bookmark == null) return;

        CategoryNameLabel.Text = Tr(bookmark.Name);

        if (!string.IsNullOrWhiteSpace(bookmark.Description))
        {
            CategoryDescriptionLabel.Text = bookmark.Description;
            CategoryDescriptionLabel.IsVisible = true;
        }
        else
        {
            CategoryDescriptionLabel.IsVisible = false;
        }
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
                await DisplayAlertAsync("Chyba načítání", $"Recepty se nepodařilo načíst.\nDetail: {ex.Message}", "OK");
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