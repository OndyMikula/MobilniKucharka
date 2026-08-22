using MobilniKucharka.Services;
using MobilniKucharka.Services.Api;
using SQLite;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MobilniKucharka.Classes.Recipe
{
    public partial class CreateRecipePage : ContentPage
    {
        private int? _currentRecipeId = null;
        private readonly bool _isEditingExisting = false;
        private readonly SQLiteAsyncConnection _db;
        private double _currentRating = 0.0;
        private string _savedImagePath = string.Empty;
        private readonly NutritionixService _nutritionixService = new();

        private bool _isLoadingRecipe = false;

        // Perzistentní objekt receptu - autosave do něj MUTUJE, místo aby pokaždé vytvářel nový Recipe()
        // a přes UpdateAsync smazal pole, která tahle stránka nespravuje (Name_EN, StepsJson_EN, SourceUrl,
        // ExternalSourceId, BookmarkId, DietaryFlagsJson...). Stejná třída bugu jako kdysi u RatingSlideru.
        private Recipe _recipe = new();

        private readonly HashSet<string> _selectedTags = [];
        private readonly Dictionary<string, Border> _tagButtons = [];

        private double _cachedProtein = 0;
        private double _cachedCarbs = 0;
        private double _cachedFat = 0;
        private double _cachedSugar = 0;
        private bool _cachedIsNutritionEstimated = false;

        private static string Tr(string csText) => MobilniKucharka.Translation.UiTranslator.Tr(csText);
        private static string CurrentLang => Preferences.Default.Get("AppLanguageCode", "cs");

        public CreateRecipePage()
        {
            InitializeComponent();
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "kucharka.db3");
            _db = new SQLiteAsyncConnection(dbPath);

            InitializeDefaultFields();
        }

        public CreateRecipePage(int recipeId) : this()
        {
            _currentRecipeId = recipeId;
            _isEditingExisting = true;
            LoadRecipeForEditing(recipeId);
        }

        private void InitializeDefaultFields()
        {
            string[] defaultTags = ["Hrnec", "Pánev", "Odměrka", "Struhadlo", "Miska", "Mísa", "Pekáč"];
            foreach (var tag in defaultTags)
            {
                AddTagButton(tag);
            }

            for (int i = 0; i < 3; i++) AddIngredientRow();
            for (int i = 0; i < 3; i++) AddStepRow();

            StarRatingHelper.Render(StarsHost, 0, starSize: 30);
        }

        private async void LoadRecipeForEditing(int id)
        {
            _isLoadingRecipe = true;
            try
            {
                var recipe = await App.Database.GetRecipeByIdAsync(id);
                if (recipe == null) return;

                _recipe = recipe; // od teď mutujeme tenhle stejný objekt, ne kopii

                _cachedProtein = recipe.Protein;
                _cachedCarbs = recipe.Carbs;
                _cachedFat = recipe.Fat;
                _cachedSugar = recipe.Sugar;
                _cachedIsNutritionEstimated = recipe.IsNutritionEstimated;

                EntryTitle.Text = recipe.Name; // jazykově správně, dřív natvrdo Name_CS
                DescriptionEditor.Text = recipe.DescriptionText;
                EntryManualCost.Text = recipe.ManualCost > 0 ? recipe.ManualCost.ToString("F0") : "";
                EntryServingSize.Text = recipe.ServingSize > 0 ? recipe.ServingSize.ToString() : string.Empty;
                EntryPrepTime.Text = recipe.PrepTime > 0 ? recipe.PrepTime.ToString() : "";

                if (!string.IsNullOrWhiteSpace(recipe.ImageUrl))
                {
                    _savedImagePath = recipe.ImageUrl;
                    RecipeImagePreview.Source = ImageSource.FromFile(recipe.ImageUrl);
                    RecipeImagePreview.IsVisible = true;
                }

                _currentRating = recipe.Rating;
                RatingSlider.Value = recipe.Rating;

                IngredientsContainer.Clear();
                if (!string.IsNullOrWhiteSpace(recipe.IngredientsRaw))
                {
                    var lines = recipe.IngredientsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|');
                        var (qty, unit) = SplitAmountForEditing(parts.ElementAtOrDefault(1) ?? "");
                        AddIngredientRow(parts.ElementAtOrDefault(0) ?? "", qty, unit);
                    }
                }
                if (IngredientsContainer.Count == 0)
                {
                    for (int i = 0; i < 3; i++) AddIngredientRow();
                }

                StepsContainer.Clear();
                var savedSteps = CurrentLang == "cs" ? recipe.Steps_CS : recipe.Steps_EN;
                if (savedSteps.Count > 0)
                {
                    foreach (var step in savedSteps)
                        AddStepRow(step);
                }
                else
                {
                    for (int i = 0; i < 3; i++) AddStepRow();
                }

                _selectedTags.Clear();
                foreach (var tag in recipe.Equipment)
                {
                    if (_tagButtons.TryGetValue(tag, out var existingChip))
                    {
                        var label = (Label)existingChip.Content!;
                        SetTagButtonSelected(existingChip, label, true);
                        _selectedTags.Add(tag);
                    }
                    else
                    {
                        AddTagButton(tag, isSelected: true);
                    }
                }
            }
            finally
            {
                _isLoadingRecipe = false;
            }
        }

        private void OnManualCostTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewTextValue)) return;

            string filtered = new([.. e.NewTextValue.Where(char.IsDigit)]);
            if (filtered != e.NewTextValue)
            {
                EntryManualCost.Text = filtered;
            }
        }

        [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)\s*(.*)$")]
        private static partial Regex AmountSplitRegexGen();

        private static (string Quantity, string Unit) SplitAmountForEditing(string amount)
        {
            if (string.IsNullOrWhiteSpace(amount)) return ("", "g");

            var match = AmountSplitRegexGen().Match(amount.Trim());
            if (!match.Success) return (amount, ""); // volný text (např. "podle chuti") -> žádná jednotka se nevymýšlí

            string quantity = match.Groups[1].Value;
            string unitRaw = match.Groups[2].Value.Trim().ToLowerInvariant();

            string unit = unitRaw switch
            {
                "kg" => "kg",
                "ml" => "ml",
                "l" => "l",
                var u when u.Contains("lžíce") || u.Contains("tbsp") => "lžíce",
                var u when u.Contains("lžička") || u.Contains("tsp") => "lžička",
                var u when u.Contains("ks") || u.Contains("kus") => "ks",
                "x" => "ks",
                "" => "ks",
                _ => "g"
            };

            return (quantity, unit);
        }

        private void AddTagButton(string tagName, bool isSelected = false)
        {
            var label = new Label
            {
                Text = tagName,
                FontSize = 12,
                TextColor = Color.FromArgb("#2196F3"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };

            var chip = new Border
            {
                BackgroundColor = Colors.Transparent,
                Stroke = Color.FromArgb("#2196F3"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 15 },
                Padding = new Thickness(15, 6),
                Margin = new Thickness(0, 0, 8, 8),
                Content = label
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                ToggleTagSelection(tagName, chip, label);
                _ = TriggerAutoSaveAsync();
            };
            chip.GestureRecognizers.Add(tap);

            _tagButtons[tagName] = chip;
            TagsFlexLayout.Children.Add(chip);

            if (isSelected)
            {
                SetTagButtonSelected(chip, label, true);
                _selectedTags.Add(tagName);
            }
        }

        private void ToggleTagSelection(string tagName, Border chip, Label label)
        {
            if (_selectedTags.Remove(tagName))
            {
                SetTagButtonSelected(chip, label, false);
            }
            else
            {
                _selectedTags.Add(tagName);
                SetTagButtonSelected(chip, label, true);
            }
        }

        private static void SetTagButtonSelected(Border chip, Label label, bool selected)
        {
            if (selected)
            {
                chip.BackgroundColor = Color.FromArgb("#2196F3");
                label.TextColor = Colors.White;
                chip.StrokeThickness = 0;
            }
            else
            {
                chip.BackgroundColor = Colors.Transparent;
                label.TextColor = Color.FromArgb("#2196F3");
                chip.StrokeThickness = 1;
            }
        }

        private void OnCreateTagClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(NewTagEntry.Text))
            {
                AddTagButton(NewTagEntry.Text.Trim());
                NewTagEntry.Text = string.Empty;
            }
        }

        private async void OnSelectImageLocal(object sender, EventArgs e)
        {
            try
            {
				var results = await MediaPicker.Default.PickPhotosAsync();
				var result = results.FirstOrDefault();
				if (result != null)
                {
                    string localFileName = $"{Guid.NewGuid()}_{result.FileName}";
                    string localFilePath = Path.Combine(FileSystem.AppDataDirectory, localFileName);

                    using Stream sourceStream = await result.OpenReadAsync();
                    using FileStream localFileStream = File.OpenWrite(localFilePath);
                    await sourceStream.CopyToAsync(localFileStream);

                    _savedImagePath = localFilePath;
                    RecipeImagePreview.Source = ImageSource.FromFile(localFilePath);
                    RecipeImagePreview.IsVisible = true;

                    await TriggerAutoSaveAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(Tr("Chyba"), $"{Tr("Nepodařilo se načíst obrázek")}: {ex.Message}", "OK");
            }
        }

        private void OnRatingValueChanged(object sender, ValueChangedEventArgs e)
        {
            double roundedValue = Math.Round(e.NewValue * 2) / 2;

            if (RatingSlider.Value != roundedValue)
            {
                RatingSlider.Value = roundedValue;
                return;
            }

            _currentRating = roundedValue;
            RatingTextLabel.Text = string.Format(Tr("Hodnocení: {0:F1} / 5"), _currentRating);

            StarRatingHelper.Render(StarsHost, _currentRating, starSize: 30);

            _ = TriggerAutoSaveAsync();
        }

        private static readonly string[] UnitOptions = ["g", "kg", "ml", "l", "lžíce", "lžička", "ks"];

        private void AddIngredientRow(string initialName = "", string initialAmount = "", string initialUnit = "g")
        {
            var grid = new Grid
            {
                ColumnDefinitions =
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(GridLength.Auto)
        }
            };

            var nameEntry = new Entry { Placeholder = Tr("Název ingredience"), Text = initialName };
            nameEntry.TextChanged += OnFieldChanged;

            var amountEntry = new Entry { Placeholder = Tr("Množství"), WidthRequest = 70, Keyboard = Keyboard.Numeric, Text = initialAmount };
            amountEntry.TextChanged += OnFieldChanged;

            var unitPicker = new Picker { WidthRequest = 90, ItemsSource = UnitOptions, SelectedItem = initialUnit };
            unitPicker.SelectedIndexChanged += (s, e) => _ = TriggerAutoSaveAsync();

            grid.Add(nameEntry, 0);
            grid.Add(amountEntry, 1);
            grid.Add(unitPicker, 2);
            IngredientsContainer.Add(grid);
        }

        private void AddStepRow(string initialText = "")
        {
            var grid = new Grid
            {
                ColumnDefinitions =
        {
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(GridLength.Auto)
        }
            };

            var emoji = new Label { Text = "👉", VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 5, 0) };
            var stepEntry = new Entry { Placeholder = Tr("Popiš tento krok..."), Text = initialText };
            stepEntry.TextChanged += OnFieldChanged;

            var upButton = new Button { Text = "▲", FontSize = 12, WidthRequest = 36, HeightRequest = 36, Padding = 0, Margin = new Thickness(4, 0, 0, 0) };
            var downButton = new Button { Text = "▼", FontSize = 12, WidthRequest = 36, HeightRequest = 36, Padding = 0, Margin = new Thickness(4, 0, 0, 0) };

            upButton.Clicked += (s, e) => MoveStepRow(grid, -1);
            downButton.Clicked += (s, e) => MoveStepRow(grid, 1);

            grid.Add(emoji, 0);
            grid.Add(stepEntry, 1);
            grid.Add(upButton, 2);
            grid.Add(downButton, 3);
            StepsContainer.Add(grid);
        }

        private void MoveStepRow(Grid row, int direction)
        {
            int index = StepsContainer.Children.IndexOf(row);
            int newIndex = index + direction;

            if (newIndex < 0 || newIndex >= StepsContainer.Children.Count) return;

            StepsContainer.Children.RemoveAt(index);
            StepsContainer.Children.Insert(newIndex, row);

            _ = TriggerAutoSaveAsync();
        }

        private void OnAddIngredientFieldClicked(object sender, EventArgs e) => AddIngredientRow();
        private void OnAddStepFieldClicked(object sender, EventArgs e) => AddStepRow();

        private List<(string Name, string Amount)> CollectIngredientRows()
        {
            var result = new List<(string, string)>();

            foreach (var child in IngredientsContainer.Children)
            {
                if (child is Grid grid && grid.Children.Count >= 3)
                {
                    string name = (grid.Children[0] as Entry)?.Text?.Trim() ?? "";
                    string quantity = (grid.Children[1] as Entry)?.Text?.Trim() ?? "";
                    string unit = (grid.Children[2] as Picker)?.SelectedItem as string ?? "g";

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(quantity)) continue;

                    // Jednotku přidáváme jen tehdy, když je v množství skutečně číslo -
                    // u volného textu (např. "podle chuti") by přidaná jednotka nedávala smysl a jen by se hromadila při každé úpravě.
                    string combinedAmount = quantity.Any(char.IsDigit) ? $"{quantity} {unit}" : quantity;

                    result.Add((name, combinedAmount));
                }
            }

            return result;
        }

        private List<string> CollectStepRows()
        {
            var result = new List<string>();

            foreach (var child in StepsContainer.Children)
            {
                if (child is Grid grid && grid.Children.Count >= 2)
                {
                    string step = (grid.Children[1] as Entry)?.Text?.Trim() ?? "";
                    if (!string.IsNullOrWhiteSpace(step))
                        result.Add(step);
                }
            }

            return result;
        }

        private async Task<(double Protein, double Carbs, double Fat, double Sugar, bool IsEstimated)> CalculateNutritionFromIngredientsAsync(List<(string Name, string Amount)> ingredientRows)
        {
            if (ingredientRows.Count == 0) return (0, 0, 0, 0, false);

            string queryText = string.Join(", ", ingredientRows.Select(i =>
                string.IsNullOrWhiteSpace(i.Amount) ? i.Name : $"{i.Amount} {i.Name}"));

            var parsed = await _nutritionixService.ParseNaturalTextAsync(queryText);

            if (parsed != null && parsed.Count > 0)
            {
                double protein = Math.Round(parsed.Sum(p => p.Protein), 1);
                double carbs = Math.Round(parsed.Sum(p => p.Carbs), 1);
                double fat = Math.Round(parsed.Sum(p => p.Fat), 1);
                double sugar = Math.Round(parsed.Sum(p => p.Sugar), 1);
                return (protein, carbs, fat, sugar, false);
            }

            var (Protein, Carbs, Fat, Sugar) = NutritionEstimationService.EstimateNutrition(ingredientRows);
            return (Protein, Carbs, Fat, Sugar, true);
        }

        private void OnPrepTimeTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewTextValue)) return;

            string filtered = new([.. e.NewTextValue.Where(char.IsDigit)]);
            if (filtered != e.NewTextValue)
            {
                EntryPrepTime.Text = filtered;
            }
        }

        private async void OnFieldChanged(object? sender, TextChangedEventArgs e)
        {
            await TriggerAutoSaveAsync();
        }

        private async Task TriggerAutoSaveAsync()
        {
            if (_isLoadingRecipe) return; // recept se ještě načítá k úpravě -> nesmíme přepsat data polovičně natažené kopie

            var ingredientRows = CollectIngredientRows();
            var stepRows = CollectStepRows();
            double manualCost = double.TryParse(EntryManualCost.Text, out var parsedCost) ? parsedCost : 0;
            string currentLang = CurrentLang;

            _recipe.ImageUrl = _savedImagePath;
            _recipe.IsDraft = !_isEditingExisting; // úprava hotového receptu ho autosave nesmí "vrátit" zpět mezi koncepty
            _recipe.Category = "Vytvořené recepty";
            _recipe.Rating = _currentRating;
            _recipe.ManualCost = manualCost;
            _recipe.ServingSize = int.TryParse(EntryServingSize.Text, out var draftServings) && draftServings > 0 ? draftServings : 0;
            _recipe.DescriptionText = DescriptionEditor.Text ?? string.Empty;
            _recipe.IngredientsRaw = string.Join("\n", ingredientRows.Select(i => $"{i.Name}|{i.Amount}"));
            _recipe.EquipmentJson = JsonSerializer.Serialize(_selectedTags);
            _recipe.PrepTime = int.TryParse(EntryPrepTime.Text, out var draftPrepTime) ? draftPrepTime : 0;
            _recipe.Protein = _cachedProtein;
            _recipe.Carbs = _cachedCarbs;
            _recipe.Fat = _cachedFat;
            _recipe.Sugar = _cachedSugar;
            _recipe.IsNutritionEstimated = _cachedIsNutritionEstimated;

            string title = string.IsNullOrWhiteSpace(EntryTitle.Text) ? Tr("Rozepsaný recept") : EntryTitle.Text.Trim();
            if (currentLang == "cs")
            {
                _recipe.Name_CS = title;
                _recipe.Steps_CS = stepRows;
            }
            else
            {
                _recipe.Name_EN = title;
                _recipe.Steps_EN = stepRows;
            }

            if (_currentRecipeId == null)
            {
                await _db.InsertAsync(_recipe);
                _currentRecipeId = _recipe.Id;
                await App.Database.AddRecipeToCategoryAsync(_currentRecipeId.Value, "Koncepty");
            }
            else
            {
                _recipe.Id = _currentRecipeId.Value;
                await _db.UpdateAsync(_recipe);
            }
        }

        private void OnDeleteRecipeButtonClicked(object sender, EventArgs e)
        {
            DeleteOverlay.IsVisible = true;
        }

        private void OnCancelDeleteClicked(object sender, EventArgs e)
        {
            DeleteOverlay.IsVisible = false;
        }

        private async void OnConfirmDeleteClicked(object sender, EventArgs e)
        {
            DeleteOverlay.IsVisible = false;

            if (_currentRecipeId != null)
            {
                await App.Database.DeleteRecipeAsync(_currentRecipeId.Value);
            }

            await Navigation.PopAsync();
        }

        private async void OnSaveRecipeClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntryTitle.Text))
            {
                await DisplayAlertAsync(Tr("Upozornění"), Tr("Zadej prosím název receptu."), "OK");
                return;
            }

            try
            {
                var ingredientRows = CollectIngredientRows();
                var stepRows = CollectStepRows();

                await SyncIngredientUnitsAsync(ingredientRows);

                var (Protein, Carbs, Fat, Sugar, IsEstimated) = await CalculateNutritionFromIngredientsAsync(ingredientRows);
                _cachedProtein = Protein;
                _cachedCarbs = Carbs;
                _cachedFat = Fat;
                _cachedSugar = Sugar;
                _cachedIsNutritionEstimated = IsEstimated;

                double manualCost = double.TryParse(EntryManualCost.Text, out var parsedCost) ? parsedCost : 0;
                string currentLang = CurrentLang;

                _recipe.ImageUrl = _savedImagePath;
                _recipe.IsDraft = false;
                _recipe.Category = "Vytvořené recepty";
                _recipe.Rating = _currentRating;
                _recipe.ManualCost = manualCost;
                _recipe.DescriptionText = DescriptionEditor.Text ?? string.Empty;
                _recipe.IsNutritionEstimated = _cachedIsNutritionEstimated;
                _recipe.ServingSize = int.TryParse(EntryServingSize.Text, out var servings) && servings > 0 ? servings : 0;
                _recipe.IngredientsRaw = string.Join("\n", ingredientRows.Select(i => $"{i.Name}|{i.Amount}"));
                _recipe.EquipmentJson = JsonSerializer.Serialize(_selectedTags);
                _recipe.PrepTime = int.TryParse(EntryPrepTime.Text, out var prepTime) ? prepTime : 0;
                _recipe.Protein = Protein;
                _recipe.Carbs = Carbs;
                _recipe.Fat = Fat;
                _recipe.Sugar = Sugar;

                string title = EntryTitle.Text.Trim();
                if (currentLang == "cs")
                {
                    _recipe.Name_CS = title;
                    _recipe.Steps_CS = stepRows;
                }
                else
                {
                    _recipe.Name_EN = title;
                    _recipe.Steps_EN = stepRows;
                }

                if (_currentRecipeId == null)
                {
                    await _db.InsertAsync(_recipe);
                    _currentRecipeId = _recipe.Id;
                }
                else
                {
                    _recipe.Id = _currentRecipeId.Value;
                    await _db.UpdateAsync(_recipe);
                }

                await App.Database.AddRecipeToCategoryAsync(_currentRecipeId.Value, "Vytvořené recepty");
                await App.Database.RemoveRecipeFromCategoryAsync(_currentRecipeId.Value, "Koncepty");

                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(Tr("Chyba při ukládání"), $"{Tr("Recept se nepodařilo uložit.")}\n{Tr("Detail")}: {ex.Message}", "OK");
            }
        }

        private static string NormalizeToBaseUnit(string pickerUnit) => pickerUnit switch
        {
            "kg" => "g",
            "l" or "lžíce" or "lžička" => "ml",
            "ks" => "ks",
            _ => "g"
        };

        private static async Task SyncIngredientUnitsAsync(List<(string Name, string Amount)> ingredientRows)
        {
            foreach (var (Name, Amount) in ingredientRows)
            {
                var parts = Amount.Split(' ', 2);
                if (parts.Length < 2) continue;

                string baseUnit = NormalizeToBaseUnit(parts[1].Trim());
                var product = await App.Database.GetOrCreateLocalProductByNameAsync(Name, baseUnit);

                if (product.Unit != baseUnit)
                {
                    await App.Database.SetProductUnitAsync(product.Id, baseUnit);
                }
            }
        }

        private void OnPageUnloaded(object sender, EventArgs e) { }
    }
}
