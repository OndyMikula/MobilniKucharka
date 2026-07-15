using MobilniKucharka.Classes;

namespace MobilniKucharka;

public partial class RecipeDetailPage : ContentPage
{
    private string? _recipeUrl;

    public RecipeDetailPage(Recipe recipe)
    {
        InitializeComponent();

        // Předání dat do XAMLu
        BindingContext = recipe;
        _recipeUrl = recipe.SourceUrl;

        SourceLinkLabel.IsVisible = !string.IsNullOrEmpty(_recipeUrl);
    }

    private async void OnUrlTapped(object sender, TappedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_recipeUrl))
        {
            try
            {
                // Otevře odkaz ve výchozím prohlížeči telefonu/počítače
                await Browser.Default.OpenAsync(new Uri(_recipeUrl), BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception)
            {
                await DisplayAlert("Chyba", "Nepodařilo se otevřít odkaz.", "OK");
            }
        }
    }
}