using MobilniKucharka.Services;

namespace MobilniKucharka.Classes.UserData.Bookmark;

public partial class CreateBookmarkPage : ContentPage
{
    private string _selectedImagePath = string.Empty;
    private bool _imageWasRemoved = false;
    private readonly bool _isEditingExisting = false;
    private readonly string _originalCategoryName = string.Empty;
    private readonly bool _isProtectedBookmark = false;

    private static readonly string[] ProtectedBookmarkNames = ["Oblíbené", "Vytvořené recepty", "Vyhledané recepty", "Koncepty"];

    private static string Tr(string csText) => MobilniKucharka.Translation.UiTranslator.Tr(csText);

    public CreateBookmarkPage()
    {
        InitializeComponent();
    }

    public CreateBookmarkPage(string categoryName) : this()
    {
        _isEditingExisting = true;
        _originalCategoryName = categoryName;
        _isProtectedBookmark = ProtectedBookmarkNames.Contains(categoryName);

        Title = Tr("Upravit záložku");
        HeaderLabel.Text = Tr("Upravit záložku");
        SaveButton.Text = Tr("Uložit změny");

        _ = LoadExistingBookmarkAsync(categoryName);
    }

    private async Task LoadExistingBookmarkAsync(string categoryName)
    {
        var bookmark = await App.Database.GetBookmarkByNameAsync(categoryName);
        if (bookmark == null) return;

        BookmarkNameEntry.Text = _isProtectedBookmark ? Tr(bookmark.Name) : bookmark.Name;
        BookmarkNameEntry.IsEnabled = !_isProtectedBookmark;
        DescriptionEditor.Text = bookmark.Description;

        if (bookmark.UseImageAsBackground)
        {
            _selectedImagePath = bookmark.BackgroundImage;
            BookmarkImagePreview.Source = ImageSource.FromFile(bookmark.BackgroundImage);
            BookmarkImagePreview.IsVisible = true;
            DefaultStateLayout.IsVisible = false;
            RemoveImageButton.IsVisible = true;
        }
    }

    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        try
        {
            var results = await MediaPicker.Default.PickPhotosAsync();
            var result = results.FirstOrDefault();
            if (result != null)
            {
                // Obrázek se rovnou zmenší přes ImageResizeService (viz tam) - fotka z fotoaparátu
                // ve full rozlišení by se v Blazor <img> (BookmarksPage.razor) zobrazovala jako
                // base64 data: URI, kde Android WebView u příliš velkých obrázků obrázek občas
                // vůbec nevykreslí, potichu, bez chyby. Výstup je vždy JPEG bez ohledu na vstupní
                // formát, proto přípona vždy ".jpg", ne původní result.FileName.
                string localFileName = $"{Guid.NewGuid()}.jpg";
                string localFilePath = Path.Combine(FileSystem.AppDataDirectory, localFileName);

                using Stream sourceStream = await result.OpenReadAsync();
                await ImageResizeService.SaveResizedAsync(sourceStream, localFilePath);

                _selectedImagePath = localFilePath;
                _imageWasRemoved = false;
                BookmarkImagePreview.Source = ImageSource.FromFile(localFilePath);

                BookmarkImagePreview.IsVisible = true;
                DefaultStateLayout.IsVisible = false;
                RemoveImageButton.IsVisible = true;
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

    private void OnRemoveImageClicked(object sender, EventArgs e)
    {
        _selectedImagePath = string.Empty;
        _imageWasRemoved = true;

        BookmarkImagePreview.Source = null;
        BookmarkImagePreview.IsVisible = false;
        DefaultStateLayout.IsVisible = true;
        RemoveImageButton.IsVisible = false;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        string? folderName = BookmarkNameEntry.Text?.Trim();
        string description = DescriptionEditor.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(folderName))
        {
            await DisplayAlertAsync("Upozornění", "Název složky nesmí být prázdný.", "OK");
            return;
        }

        try
        {
            if (_isEditingExisting)
            {
                await App.Database.UpdateBookmarkAsync(_originalCategoryName, folderName, _selectedImagePath, _imageWasRemoved, description);
            }
            else
            {
                await App.Database.InsertNewCategoryAsync(folderName, _selectedImagePath, description);
            }

            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Chyba", $"Nepodařilo se uložit složku: {ex.Message}", "OK");
        }
    }
}