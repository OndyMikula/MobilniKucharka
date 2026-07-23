using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using MobilniKucharka.Classes;
using MobilniKucharka.Classes.UserData.Bookmark;
using MobilniKucharka.Services;

namespace MobilniKucharka.Classes.Recipe;

public partial class RecipeDetailPage : ContentPage
{
    private readonly RecipeWithCost _recipeWithCost;

    public RecipeDetailPage(RecipeWithCost selectedItem)
    {
        InitializeComponent();
        _recipeWithCost = selectedItem;
        BindingContext = _recipeWithCost;

        RecipeImage.Source = _recipeWithCost.Recipe.ImageUrl;
        RecipeNameLabel.Text = _recipeWithCost.Recipe.Name_CS;
        HeroRatingNumberLabel.Text = _recipeWithCost.Recipe.Rating.ToString("F1");
        StarRatingHelper.Render(HeroStarsHost, _recipeWithCost.Recipe.Rating);
        SetupUserRatingWidget();

        ProteinLabel.Text = $"{_recipeWithCost.Recipe.Protein}g";
        CarbsLabel.Text = $"{_recipeWithCost.Recipe.Carbs}g";
        FatLabel.Text = $"{_recipeWithCost.Recipe.Fat}g";
        SugarLabel.Text = $"{_recipeWithCost.Recipe.Sugar}g";
        NutritionEstimateLabel.IsVisible = _recipeWithCost.Recipe.IsNutritionEstimated;

        LoadIngredientsAndSteps();
        InitializeFavoriteStateAsync();

        if (_recipeWithCost.Recipe.PrepTime > 0)
        {
            PrepTimeLabel.Text = $"⏱ {_recipeWithCost.Recipe.PrepTime} min";
            PrepTimeLabel.IsVisible = true;
        }
    }

    private async void LoadIngredientsAndSteps()
    {
        var service = App.Database;
        var recipe = _recipeWithCost.Recipe;

        int peopleCount = Preferences.Default.Get("PeopleCount", 2);
        string shopColumn = Preferences.Default.Get("ShopColName", "PriceLidl");

        var ingredients = await service.GetIngredientsForRecipeAsync(recipe.Id, peopleCount);

        if (!string.IsNullOrWhiteSpace(recipe.DescriptionText))
        {
            DescriptionSection.IsVisible = true;
            DescriptionLabel.Text = recipe.DescriptionText;
        }

        if (recipe.Equipment.Count > 0)
        {
            EquipmentSection.IsVisible = true;
            EquipmentFlexLayout.Children.Clear();
            foreach (var tag in recipe.Equipment)
            {
                EquipmentFlexLayout.Children.Add(new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Stroke = Color.FromArgb("#2196F3"),
                    BackgroundColor = Colors.Transparent,
                    Padding = new Thickness(12, 6),
                    Margin = new Thickness(0, 0, 8, 8),
                    Content = new Label { Text = tag, TextColor = Color.FromArgb("#2196F3"), FontSize = 12 }
                });
            }
        }

        // Recept bez napojení na LocalProduct katalog (vlastní recept, MealDB, Spoonacular) -> zobrazíme aspoň napsaný text
        if (ingredients.Count == 0 && !string.IsNullOrWhiteSpace(recipe.IngredientsRaw))
        {
            ingredients = [.. recipe.IngredientsRaw
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    var parts = line.Split('|');
                    return new DisplayIngredient
                    {
                        Name = parts.ElementAtOrDefault(0)?.Trim() ?? "",
                        AmountText = parts.ElementAtOrDefault(1)?.Trim() ?? "",
                        CostText = "Cena neznámá"
                    };
                })
                .Where(i => !string.IsNullOrWhiteSpace(i.Name))];
        }

        BindableLayout.SetItemsSource(IngredientsLayout, ingredients);

        string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
        var rawSteps = currentLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;

        var structuredSteps = rawSteps.Select((stepText, index) => new DisplayStep
        {
            StepNumber = index + 1,
            StepText = stepText
        }).ToList();

        BindableLayout.SetItemsSource(StepsLayout, structuredSteps);

        TotalPriceLabel.Text = _recipeWithCost.CalculatedCost > 0
            ? $"Celkem za jídlo: {_recipeWithCost.CalculatedCost:N0} Kč"
            : "Cena nákupu není k dispozici";
        PeopleCountBadge.Text = $"({peopleCount} os.)";
    }

    private async void OnIngredientTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Grid grid || grid.BindingContext is not DisplayIngredient ingredient) return;

        string action = await DisplayActionSheet(ingredient.Name, "Zrušit", null,
            "Zadat vlastní cenu", "Propojit s existující surovinou");

        if (action == "Zadat vlastní cenu")
        {
            string result = await DisplayPromptAsync(
                "Vlastní cena suroviny",
                $"Zadej cenu za jednotku pro \"{ingredient.Name}\" (Kč), nebo nech prázdné pro průměrnou cenu.",
                "Uložit", "Zrušit", keyboard: Keyboard.Numeric);

            if (result == null) return;

            if (string.IsNullOrWhiteSpace(result))
                await App.Database.ClearManualPriceAsync(ingredient.ProductId);
            else if (double.TryParse(result.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture, out var price))
                await App.Database.SetManualPriceAsync(ingredient.ProductId, price);

            LoadIngredientsAndSteps();
        }
        else if (action == "Propojit s existující surovinou")
        {
            var allProducts = await App.Database.GetAllLocalProductsAsync();
            string[] names = [.. allProducts.Select(p => p.Name_CS)];

            string chosen = await DisplayActionSheet("Propojit s...", "Zrušit", null, names);
            var match = allProducts.FirstOrDefault(p => p.Name_CS == chosen);

            if (match != null)
            {
                await App.Database.LinkIngredientNameToProductAsync(ingredient.Name, match.Id);
                LoadIngredientsAndSteps();
            }
        }
    }

    private async void InitializeFavoriteStateAsync()
    {
        var categories = await App.Database.GetCategoriesForRecipeAsync(_recipeWithCost.Recipe.Id);
        bool isFavorite = categories.Contains("Oblíbené");
        FavoriteIcon.Text = isFavorite ? "♥" : "♡";
        FavoriteIcon.TextColor = isFavorite ? Colors.Red : Colors.White;
    }

    private async void OnFavoriteToggled(object sender, TappedEventArgs e)
    {
        bool isCurrentlyFavorite = FavoriteIcon.Text == "♥";

        if (isCurrentlyFavorite)
        {
            await App.Database.RemoveRecipeFromCategoryAsync(_recipeWithCost.Recipe.Id, "Oblíbené");
            FavoriteIcon.Text = "♡";
            FavoriteIcon.TextColor = Colors.White;
        }
        else
        {
            await App.Database.AddRecipeToCategoryAsync(_recipeWithCost.Recipe.Id, "Oblíbené");
            FavoriteIcon.Text = "♥";
            FavoriteIcon.TextColor = Colors.Red;
        }
    }

    private async void OnOpenBookmarksClicked(object sender, EventArgs e)
    {
        var allCategories = await App.Database.GetDistinctCategoriesAsync();
        var currentRecipeCategories = await App.Database.GetCategoriesForRecipeAsync(_recipeWithCost.Recipe.Id);

        var selectionList = new List<BookmarkSelectionModel>();
        foreach (var cat in allCategories)
        {
            selectionList.Add(new BookmarkSelectionModel
            {
                CategoryName = cat,
                IsRecipeInCategory = currentRecipeCategories.Contains(cat)
            });
        }

        BookmarkSelectionLayout.ItemsSource = selectionList;
        BookmarkOverlay.IsVisible = true;
    }

    private void OnCloseBookmarkOverlayClicked(object sender, EventArgs e)
    {
        BookmarkOverlay.IsVisible = false;
        _ = RefreshFavoriteIconAsync(); // kdyby se "Oblíbené" změnilo přes tenhle seznam, srdíčko se dorovná
    }

    private async Task RefreshFavoriteIconAsync()
    {
        var categories = await App.Database.GetCategoriesForRecipeAsync(_recipeWithCost.Recipe.Id);
        bool isFavorite = categories.Contains("Oblíbené");
        FavoriteIcon.Text = isFavorite ? "♥" : "♡";
        FavoriteIcon.TextColor = isFavorite ? Colors.Red : Colors.White;
    }

    private async void OnBookmarkCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (((CheckBox)sender).BindingContext is BookmarkSelectionModel changedBookmark)
        {
            if (e.Value)
                await App.Database.AddRecipeToCategoryAsync(_recipeWithCost.Recipe.Id, changedBookmark.CategoryName);
            else
                await App.Database.RemoveRecipeFromCategoryAsync(_recipeWithCost.Recipe.Id, changedBookmark.CategoryName);
        }
    }

    private async void OnEditRecipeClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new CreateRecipePage(_recipeWithCost.Recipe.Id));
    }

    private async void OnOptionsMenuClicked(object sender, TappedEventArgs e)
    {
        string action = await DisplayActionSheet("Možnosti receptu", "Zrušit", null, "Přidat do záložky", "Upravit recept", "Smazat recept");

        switch (action)
        {
            case "Přidat do záložky":
                OnOpenBookmarksClicked(this, EventArgs.Empty);
                break;
            case "Upravit recept":
                await Navigation.PushAsync(new CreateRecipePage(_recipeWithCost.Recipe.Id));
                break;
            case "Smazat recept":
                DeleteOverlay.IsVisible = true;
                break;
        }
    }

    private void OnCancelDeleteClicked(object sender, EventArgs e)
    {
        DeleteOverlay.IsVisible = false;
    }

    private async void OnConfirmDeleteClicked(object sender, EventArgs e)
    {
        DeleteOverlay.IsVisible = false;
        await App.Database.DeleteRecipeAsync(_recipeWithCost.Recipe.Id);
        await Navigation.PopAsync();
    }

    private void SetupUserRatingWidget()
    {
        UserRatingSlider.Value = _recipeWithCost.Recipe.Rating;
        StarRatingHelper.Render(UserRatingStarsHost, _recipeWithCost.Recipe.Rating, starSize: 32);
    }

    private async void OnUserRatingSliderChanged(object sender, ValueChangedEventArgs e)
    {
        double roundedValue = Math.Round(e.NewValue * 2) / 2;

        if (UserRatingSlider.Value != roundedValue)
        {
            UserRatingSlider.Value = roundedValue;
            return;
        }

        _recipeWithCost.Recipe.Rating = roundedValue;
        HeroRatingNumberLabel.Text = roundedValue.ToString("F1");
        StarRatingHelper.Render(HeroStarsHost, roundedValue);
        StarRatingHelper.Render(UserRatingStarsHost, roundedValue, starSize: 32);

        await App.Database.UpdateRecipeRatingAsync(_recipeWithCost.Recipe.Id, roundedValue);
    }
}

public class DisplayStep
{
    public int StepNumber { get; set; }
    public string StepText { get; set; } = string.Empty;
}