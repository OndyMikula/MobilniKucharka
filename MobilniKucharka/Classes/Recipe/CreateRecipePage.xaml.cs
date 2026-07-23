using MobilniKucharka.Services;
using MobilniKucharka.Services.Api;
using SQLite;
using System.Text.Json;

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

        private readonly HashSet<string> _selectedTags = [];
        private readonly Dictionary<string, Button> _tagButtons = [];

        private double _cachedProtein = 0;
        private double _cachedCarbs = 0;
        private double _cachedFat = 0;
        private double _cachedSugar = 0;
        private bool _cachedIsNutritionEstimated = false;

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
            var recipe = await App.Database.GetRecipeByIdAsync(id);
            if (recipe == null) return;

            _cachedProtein = recipe.Protein;
            _cachedCarbs = recipe.Carbs;
            _cachedFat = recipe.Fat;
            _cachedSugar = recipe.Sugar;
            _cachedIsNutritionEstimated = recipe.IsNutritionEstimated;

            EntryTitle.Text = recipe.Name_CS;
            DescriptionEditor.Text = recipe.DescriptionText;
            EntryManualCost.Text = recipe.ManualCost > 0 ? recipe.ManualCost.ToString("F0") : "";

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
                    AddIngredientRow(parts.ElementAtOrDefault(0) ?? "", parts.ElementAtOrDefault(1) ?? "");
                }
            }
            if (IngredientsContainer.Count == 0)
            {
                for (int i = 0; i < 3; i++) AddIngredientRow();
            }

            StepsContainer.Clear();
            var savedSteps = recipe.Steps_CS;
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
                if (_tagButtons.TryGetValue(tag, out var existingBtn))
                {
                    SetTagButtonSelected(existingBtn, true);
                    _selectedTags.Add(tag);
                }
                else
                {
                    AddTagButton(tag, isSelected: true);
                }
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

        private void AddTagButton(string tagName, bool isSelected = false)
        {
            var btn = new Button
            {
                Text = tagName,
                BackgroundColor = Colors.Transparent,
                BorderColor = Color.FromArgb("#2196F3"),
                BorderWidth = 1,
                TextColor = Color.FromArgb("#2196F3"),
                CornerRadius = 15,
                HeightRequest = 35,
                Padding = new Thickness(15, 0),
                Margin = new Thickness(0, 0, 8, 8),
                FontSize = 12
            };

            btn.Clicked += (s, e) =>
            {
                ToggleTagSelection(tagName, btn);
                _ = TriggerAutoSaveAsync();
            };

            _tagButtons[tagName] = btn;
            TagsFlexLayout.Children.Add(btn);

            if (isSelected)
            {
                SetTagButtonSelected(btn, true);
                _selectedTags.Add(tagName);
            }
        }

        private void ToggleTagSelection(string tagName, Button btn)
        {
            if (_selectedTags.Remove(tagName))
            {
                SetTagButtonSelected(btn, false);
            }
            else
            {
                _selectedTags.Add(tagName);
                SetTagButtonSelected(btn, true);
            }
        }

        private static void SetTagButtonSelected(Button btn, bool selected)
        {
            if (selected)
            {
                btn.BackgroundColor = Color.FromArgb("#2196F3");
                btn.TextColor = Colors.White;
                btn.BorderWidth = 0;
            }
            else
            {
                btn.BackgroundColor = Colors.Transparent;
                btn.TextColor = Color.FromArgb("#2196F3");
                btn.BorderWidth = 1;
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
                var result = await MediaPicker.Default.PickPhotoAsync();
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
                await DisplayAlert("Chyba", $"Nepodařilo se načíst obrázek: {ex.Message}", "OK");
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
            RatingTextLabel.Text = $"Hodnocení: {_currentRating:F1} / 5";

            StarRatingHelper.Render(StarsHost, _currentRating, starSize: 30);

            _ = TriggerAutoSaveAsync();
        }

        private void AddIngredientRow(string initialName = "", string initialAmount = "")
        {
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            var nameEntry = new Entry { Placeholder = "Název ingredience", Text = initialName };
            nameEntry.TextChanged += OnFieldChanged;
            var amountEntry = new Entry { Placeholder = "Množství (např. 100g)", WidthRequest = 150, Text = initialAmount };
            amountEntry.TextChanged += OnFieldChanged;

            grid.Add(nameEntry, 0);
            grid.Add(amountEntry, 1);
            IngredientsContainer.Add(grid);
        }

        private void AddStepRow(string initialText = "")
        {
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
            var emoji = new Label { Text = "👉", VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 5, 0) };
            var stepEntry = new Entry { Placeholder = "Popiš tento krok...", Text = initialText };
            stepEntry.TextChanged += OnFieldChanged;

            grid.Add(emoji, 0);
            grid.Add(stepEntry, 1);
            StepsContainer.Add(grid);
        }

        private void OnAddIngredientFieldClicked(object sender, EventArgs e) => AddIngredientRow();
        private void OnAddStepFieldClicked(object sender, EventArgs e) => AddStepRow();

        private List<(string Name, string Amount)> CollectIngredientRows()
        {
            var result = new List<(string, string)>();

            foreach (var child in IngredientsContainer.Children)
            {
                if (child is Grid grid && grid.Children.Count >= 2)
                {
                    string name = (grid.Children[0] as Entry)?.Text?.Trim() ?? "";
                    string amount = (grid.Children[1] as Entry)?.Text?.Trim() ?? "";

                    if (!string.IsNullOrWhiteSpace(name))
                        result.Add((name, amount));
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

        private async void OnFieldChanged(object? sender, TextChangedEventArgs e)
        {
            await TriggerAutoSaveAsync();
        }

        private async Task TriggerAutoSaveAsync()
        {
            var ingredientRows = CollectIngredientRows();
            var stepRows = CollectStepRows();
            double manualCost = double.TryParse(EntryManualCost.Text, out var parsedCost) ? parsedCost : 0;

            var draftRecipe = new MobilniKucharka.Classes.Recipe.Recipe
            {
                Name_CS = string.IsNullOrWhiteSpace(EntryTitle.Text) ? "Rozepsaný recept" : EntryTitle.Text.Trim(),
                ImageUrl = _savedImagePath,
                IsDraft = !_isEditingExisting, // úprava hotového receptu ho autosave nesmí "vrátit" zpět mezi koncepty
                Category = "Vytvořené recepty",
                Rating = _currentRating,
                ManualCost = manualCost,
                DescriptionText = DescriptionEditor.Text ?? string.Empty,
                IngredientsRaw = string.Join("\n", ingredientRows.Select(i => $"{i.Name}|{i.Amount}")),
                StepsJson_CS = JsonSerializer.Serialize(stepRows),
                EquipmentJson = JsonSerializer.Serialize(_selectedTags),
                Protein = _cachedProtein,
                Carbs = _cachedCarbs,
                Fat = _cachedFat,
                Sugar = _cachedSugar,
                IsNutritionEstimated = _cachedIsNutritionEstimated
            };

            if (_currentRecipeId == null)
            {
                await _db.InsertAsync(draftRecipe);
                _currentRecipeId = draftRecipe.Id;
                await App.Database.AddRecipeToCategoryAsync(_currentRecipeId.Value, "Koncepty");
            }
            else
            {
                draftRecipe.Id = _currentRecipeId.Value;
                await _db.UpdateAsync(draftRecipe);
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
                await DisplayAlert("Upozornění", "Zadej prosím název receptu.", "OK");
                return;
            }

            try
            {
                var ingredientRows = CollectIngredientRows();
                var stepRows = CollectStepRows();

                var (Protein, Carbs, Fat, Sugar, IsEstimated) = await CalculateNutritionFromIngredientsAsync(ingredientRows);
                _cachedProtein = Protein;
                _cachedCarbs = Carbs;
                _cachedFat = Fat;
                _cachedSugar = Sugar;
                _cachedIsNutritionEstimated = IsEstimated;

                double manualCost = double.TryParse(EntryManualCost.Text, out var parsedCost) ? parsedCost : 0;

                var finalRecipe = new Recipe
                {
                    Name_CS = EntryTitle.Text.Trim(),
                    ImageUrl = _savedImagePath,
                    IsDraft = false,
                    Category = "Vytvořené recepty",
                    Rating = _currentRating,
                    ManualCost = manualCost,
                    DescriptionText = DescriptionEditor.Text ?? string.Empty,
                    IsNutritionEstimated = _cachedIsNutritionEstimated,
                    IngredientsRaw = string.Join("\n", ingredientRows.Select(i => $"{i.Name}|{i.Amount}")),
                    StepsJson_CS = JsonSerializer.Serialize(stepRows),
                    EquipmentJson = JsonSerializer.Serialize(_selectedTags),
                    Protein = Protein,
                    Carbs = Carbs,
                    Fat = Fat,
                    Sugar = Sugar
                };

                if (_currentRecipeId == null)
                {
                    await _db.InsertAsync(finalRecipe);
                    _currentRecipeId = finalRecipe.Id;
                }
                else
                {
                    finalRecipe.Id = _currentRecipeId.Value;
                    await _db.UpdateAsync(finalRecipe);
                }

                await App.Database.AddRecipeToCategoryAsync(_currentRecipeId.Value, "Vytvořené recepty");
                await App.Database.RemoveRecipeFromCategoryAsync(_currentRecipeId.Value, "Koncepty");

                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Chyba při ukládání", $"Recept se nepodařilo uložit.\nDetail: {ex.Message}", "OK");
            }
        }

        private void OnPageUnloaded(object sender, EventArgs e) { }
    }
}