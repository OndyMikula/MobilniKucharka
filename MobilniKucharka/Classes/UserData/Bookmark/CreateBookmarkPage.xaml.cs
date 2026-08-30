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
            var results = await MediaPicker.Default.PickPhotosAsync();
            var result = results.FirstOrDefault();
            if (result != null)
            {
                // Stejný vzor jako CreateRecipePage.OnSelectImageLocal - MediaPicker vrací FullPath
                // do dočasného umístění (obvykle FileSystem.CacheDirectory), které nepřežije
                // aktualizaci appky ani běžné mazání cache systémem. Musíme soubor zkopírovat do
                // trvalého FileSystem.AppDataDirectory, jinak po update obrázek zmizí a zobrazí se
                // výchozí modrá barva (BackgroundColor) misto něj.
                string localFileName = $"{Guid.NewGuid()}_{result.FileName}";
                string localFilePath = Path.Combine(FileSystem.AppDataDirectory, localFileName);

                using Stream sourceStream = await result.OpenReadAsync();
                using FileStream localFileStream = File.OpenWrite(localFilePath);
                await sourceStream.CopyToAsync(localFileStream);

                _selectedImagePath = localFilePath;
                BookmarkImagePreview.Source = ImageSource.FromFile(localFilePath);

                BookmarkImagePreview.IsVisible = true;
                DefaultStateLayout.IsVisible = false;
            }
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlertAsync("Chyba", "Tato funkce není na vašem zařízení podporována.", "OK");
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync("Práva", "Aplikace nemá oprávnění přistupovat k fotkám.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Chyba", $"Obrázek se nepodařilo načíst: {ex.Message}", "OK");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        string? folderName = BookmarkNameEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(folderName))
        {
            await DisplayAlertAsync("Upozornění", "Název složky nesmí být prázdný.", "OK");
            return;
        }

        try
        {
            await App.Database.InsertNewCategoryAsync(folderName, _selectedImagePath);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Chyba", $"Nepodařilo se uložit složku: {ex.Message}", "OK");
        }
    }
}