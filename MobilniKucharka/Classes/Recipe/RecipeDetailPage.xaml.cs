using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using MobilniKucharka.Classes;
using MobilniKucharka.Classes.UserData.Bookmark;
using MobilniKucharka.Services;
using System.Text.RegularExpressions;

namespace MobilniKucharka.Classes.Recipe;

public partial class RecipeDetailPage : ContentPage
{
    private readonly RecipeWithCost _recipeWithCost;
    private bool _hasAppearedOnce = false;

    public RecipeDetailPage(RecipeWithCost selectedItem)
    {
        InitializeComponent();
        _recipeWithCost = selectedItem;
        BindingContext = _recipeWithCost;

        PopulateFromCurrentRecipe();
        InitializeFavoriteStateAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_hasAppearedOnce)
        {
            _ = RefreshFromDatabaseAsync();
        }
        _hasAppearedOnce = true;
    }

    private async Task RefreshFromDatabaseAsync()
    {
        var freshRecipe = await App.Database.GetRecipeByIdAsync(_recipeWithCost.Recipe.Id);
        if (freshRecipe == null) return;

        _recipeWithCost.Recipe = freshRecipe;
        BindingContext = _recipeWithCost;
        PopulateFromCurrentRecipe();
        await Task.Run(InitializeFavoriteStateAsync);
    }

    private void PopulateFromCurrentRecipe()
    {
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

        if (_recipeWithCost.Recipe.PrepTime > 0)
        {
            PrepTimeLabel.Text = $"⏱ {_recipeWithCost.Recipe.PrepTime} min";
            PrepTimeLabel.IsVisible = true;
        }

        if (!string.IsNullOrWhiteSpace(_recipeWithCost.Recipe.SourceUrl))
        {
            SourceLabel.Text = "🔗 Zobrazit původní recept";
            SourceLabel.TextDecorations = TextDecorations.Underline;
            SourceLabel.GestureRecognizers.Clear();
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) => await Launcher.Default.OpenAsync(_recipeWithCost.Recipe.SourceUrl);
            SourceLabel.GestureRecognizers.Add(tap);
        }
        else if (string.IsNullOrWhiteSpace(_recipeWithCost.Recipe.ExternalSourceId))
        {
            SourceLabel.Text = "Recept vytvořen uživatelem";
        }

        LoadIngredientsAndSteps();
    }

    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)")]
    private static partial Regex AmountScaleNumberRegexGen();

    private async void LoadIngredientsAndSteps()
    {
        var service = App.Database;
        var recipe = _recipeWithCost.Recipe;

        int peopleCount = Preferences.Default.Get("PeopleCount", 2);

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

        int effectiveServingSize = recipe.ServingSize > 0 ? recipe.ServingSize : 4;
        double scaleFactor = peopleCount / (double)effectiveServingSize;

        if (ingredients.Count == 0 && !string.IsNullOrWhiteSpace(recipe.IngredientsRaw))
        {
            var rawLines = recipe.IngredientsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            ingredients = [];

            foreach (var line in rawLines)
            {
                var parts = line.Split('|');
                string name = parts.ElementAtOrDefault(0)?.Trim() ?? "";
                string amount = parts.ElementAtOrDefault(1)?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(name)) continue;

                string detectedUnit = NutritionEstimationService.DetectUnitFamily(amount);
                var product = await App.Database.GetOrCreateLocalProductByNameAsync(name, detectedUnit);
                double pieceWeight = product.TypicalUnitWeightGrams > 0 ? product.TypicalUnitWeightGrams : 60;
                double? parsedAmount = NutritionEstimationService.ConvertToProductUnit(amount, product.Unit, pieceWeight);
                double? scaledAmount = parsedAmount != null ? parsedAmount.Value * scaleFactor : null;

                string costText;
                double costValue = 0;
                if (scaledAmount == null)
                {
                    costText = "";
                }
                else
                {
                    costValue = Math.Round(scaledAmount.Value * product.EffectivePrice, 0);
                    costText = costValue > 0 ? $"{costValue:N0} Kč" : "? Kč";
                }

                string displayAmount = amount;
                if (!string.IsNullOrWhiteSpace(amount) && scaleFactor != 1.0)
                {
                    var numMatch = AmountScaleNumberRegexGen().Match(amount);
                    if (numMatch.Success)
                    {
                        double originalNum = double.Parse(numMatch.Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
                        double scaledNum = originalNum * scaleFactor;
                        string restOfText = amount[numMatch.Length..];
                        displayAmount = $"{scaledNum:0.#}{restOfText}";
                    }
                }

                ingredients.Add(new DisplayIngredient
                {
                    ProductId = product.Id,
                    RawAmount = scaledAmount ?? 0,
                    CostValue = costValue,
                    Name = name,
                    AmountText = displayAmount,
                    CostText = costText
                });
            }
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

        var (totalCost, allPriced) = await App.Database.GetRecipeCostDetailsAsync(recipe.Id, peopleCount);
        TotalPriceLabel.Text = allPriced && totalCost > 0
            ? $"Celkem za jídlo: {totalCost:N0} Kč"
            : "Cena nákupu není k dispozici";

        PeopleCountBadge.Text = $"({effectiveServingSize} os.)";
    }

    private async void OnIngredientTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Grid grid || grid.BindingContext is not DisplayIngredient ingredient) return;

        if (ingredient.ProductId <= 0)
        {
            await DisplayAlert("Chyba", "Tuto surovinu se nepodařilo přiřadit k produktu. Zkus recept znovu otevřít.", "OK");
            return;
        }

        string action = await DisplayActionSheet(ingredient.Name, "Zrušit", null,
    "Zadat vlastní cenu", "Propojit s existující surovinou", "Změnit jednotku");

        if (action == "Zadat vlastní cenu")
        {
            if (ingredient.RawAmount <= 0)
            {
                string manualResult = await DisplayPromptAsync(
                    "Vlastní cena suroviny",
                    $"Množství pro \"{ingredient.Name}\" nejde rozeznat, zadej rovnou cenu za jednotku (Kč), nebo nech prázdné pro průměrnou cenu.",
                    "Uložit", "Zrušit", keyboard: Keyboard.Numeric);

                if (manualResult == null) return;

                if (string.IsNullOrWhiteSpace(manualResult))
                    await App.Database.ClearManualPriceAsync(ingredient.ProductId);
                else if (double.TryParse(manualResult.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture, out var unitPrice))
                    await App.Database.SetManualPriceAsync(ingredient.ProductId, unitPrice);

                LoadIngredientsAndSteps();
                return;
            }

            string result = await DisplayPromptAsync(
                "Vlastní cena suroviny",
                $"Kolik jsi celkem zaplatil/a za {ingredient.AmountText} suroviny \"{ingredient.Name}\"? (Kč)\nCena se přepočítá na jednotku a použije se i v ostatních receptech s jiným množstvím.",
                "Uložit", "Zrušit", keyboard: Keyboard.Numeric);

            if (result == null) return;

            if (string.IsNullOrWhiteSpace(result))
            {
                await App.Database.ClearManualPriceAsync(ingredient.ProductId);
            }
            else if (double.TryParse(result.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture, out var totalPrice))
            {
                double pricePerUnit = totalPrice / ingredient.RawAmount;
                await App.Database.SetManualPriceAsync(ingredient.ProductId, pricePerUnit);
            }
            else
            {
                await DisplayAlert("Neplatná hodnota", $"\"{result}\" se nepodařilo rozpoznat jako číslo. Zkus to znovu, jen s číslicemi (např. 25 nebo 25.50).", "OK");
                return;
            }

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
        else if (action == "Změnit jednotku")
        {
            string[] unitOptions = ["g", "ml", "ks"];
            string chosenUnit = await DisplayActionSheet("Vyber jednotku", "Zrušit", null, unitOptions);

            if (unitOptions.Contains(chosenUnit))
            {
                await App.Database.SetProductUnitAsync(ingredient.ProductId, chosenUnit);
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
        _ = RefreshFavoriteIconAsync();
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
        bool isDevMode = Preferences.Default.Get("IsDeveloperMode", false);

        string[] options = isDevMode
            ? ["Sdílet recept", "Sdílet přes odkaz", "Přidat do záložky", "Upravit recept", "Smazat recept", "🔧 Zobrazit syrová data kroků"]
            : ["Sdílet recept", "Sdílet přes odkaz", "Přidat do záložky", "Upravit recept", "Smazat recept"];

        string action = await DisplayActionSheet("Možnosti receptu", "Zrušit", null, options);

        switch (action)
        {
            case "Sdílet recept":
                await ShareRecipeAsync();
                break;
            case "Sdílet přes odkaz":
                await ShareRecipeViaLinkAsync();
                break;
            case "Přidat do záložky":
                OnOpenBookmarksClicked(this, EventArgs.Empty);
                break;
            case "Upravit recept":
                await Navigation.PushAsync(new CreateRecipePage(_recipeWithCost.Recipe.Id));
                break;
            case "Smazat recept":
                DeleteOverlay.IsVisible = true;
                break;
            case "🔧 Zobrazit syrová data kroků":
                ShowRawStepsDiagnostic();
                break;
        }
    }

    private async Task ShareRecipeAsync()
    {
        try
        {
            string filePath = await RecipeShareService.ExportRecipeAsync(_recipeWithCost.Recipe);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Sdílet recept: {_recipeWithCost.Recipe.Name_CS}",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Chyba", $"Sdílení se nepodařilo: {ex.Message}", "OK");
        }
    }

    private async Task ShareRecipeViaLinkAsync()
    {
        string? link = await RecipeLinkShareService.ShareViaLinkAsync(_recipeWithCost.Recipe);

        if (link == null)
        {
            await DisplayAlert("Chyba", "Odkaz se nepodařilo vytvořit. Zkontroluj internetové připojení.", "OK");
            return;
        }

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = $"Recept: {_recipeWithCost.Recipe.Name_CS}",
            Text = $"Podívej se na tento recept ({_recipeWithCost.Recipe.Name_CS}) v Mobilní Kuchařce: {link}\n(Odkaz je platný 24 hodin nebo dokud ho neotevřeš.)"
        });
    }

    private async void ShowRawStepsDiagnostic()
    {
        var sb = new System.Text.StringBuilder();
        var steps = _recipeWithCost.Recipe.Steps_CS;

        for (int i = 0; i < steps.Count; i++)
        {
            sb.AppendLine($"[{i}] \"{steps[i]}\"");
            foreach (char c in steps[i])
            {
                if (!char.IsLetterOrDigit(c) && c != ' ' && !char.IsPunctuation(c))
                    sb.AppendLine($"    neobvyklý znak: U+{(int)c:X4}");
            }
        }

        await DisplayAlert("Syrová data kroků", sb.Length > 0 ? sb.ToString() : "Žádné kroky.", "OK");
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