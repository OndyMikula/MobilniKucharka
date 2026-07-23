using MobilniKucharka.Services;

namespace MobilniKucharka.Classes.UserData.Bookmark;

public partial class CreateBookmarkPage : ContentPage
{
    private string _selectedImagePath = string.Empty;

    public CreateBookmarkPage()
    {
        InitializeComponent();
    }

    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Vyberte obrázek složky"
            });

            if (result != null)
            {
                _selectedImagePath = result.FullPath;
                BookmarkImagePreview.Source = ImageSource.FromFile(_selectedImagePath);

                BookmarkImagePreview.IsVisible = true;
                DefaultStateLayout.IsVisible = false;
            }
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert("Chyba", "Tato funkce není na vašem zařízení podporována.", "OK");
        }
        catch (PermissionException)
        {
            await DisplayAlert("Práva", "Aplikace nemá oprávnění přistupovat k fotkám.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Chyba", $"Obrázek se nepodařilo načíst: {ex.Message}", "OK");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        string? folderName = BookmarkNameEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(folderName))
        {
            await DisplayAlert("Upozornění", "Název složky nesmí být prázdný.", "OK");
            return;
        }

        try
        {
            await App.Database.InsertNewCategoryAsync(folderName, _selectedImagePath);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Chyba", $"Nepodařilo se uložit složku: {ex.Message}", "OK");
        }
    }
}