namespace MobilniKucharka.Classes.UserData.Bookmark;

public partial class CreateBookmarkPage : ContentPage
{
    private string _selectedImagePath = string.Empty;
    private bool _imageWasRemoved = false;
    private readonly bool _isEditingExisting = false;
    private readonly string _originalCategoryName = string.Empty;
    private readonly bool _isProtectedBookmark = false;

    // Musí odpovídat BudgetPlannerService.ProtectedBookmarkNames - kdyby appka na jedné straně
    // dovolila přejmenovat pole, ale DB metoda to potichu zablokovala, uživatel by viděl "uloženo"
    // a přitom se nic nezměnilo. Zákaz proto vynucujeme na obou místech stejně.
    private static readonly string[] ProtectedBookmarkNames = ["Oblíbené", "Vytvořené recepty", "Vyhledané recepty", "Koncepty"];

    private static string Tr(string csText) => MobilniKucharka.Translation.UiTranslator.Tr(csText);

    public CreateBookmarkPage()
    {
        InitializeComponent();
    }

    // Editační režim - otevřeno z BookmarksPage.razor přes "Upravit záložku". Název jde přejmenovat
    // jen u nechráněných (uživatelem vytvořených) záložek - viz ProtectedBookmarkNames a
    // BudgetPlannerService.UpdateBookmarkAsync, kde je stejné pravidlo vynucené i na straně DB.
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
                // Stejný vzor jako CreateRecipePage.OnSelectImageLocal - MediaPicker vrací FullPath
                // do dočasného umístění, které nepřežije aktualizaci appky ani mazání cache systémem.
                // Musíme soubor zkopírovat do trvalého FileSystem.AppDataDirectory, jinak obrázek
                // po update zmizí a zobrazí se výchozí modrá barva (BackgroundColor) místo něj.
                string localFileName = $"{Guid.NewGuid()}_{result.FileName}";
                string localFilePath = Path.Combine(FileSystem.AppDataDirectory, localFileName);

                using Stream sourceStream = await result.OpenReadAsync();
                using FileStream localFileStream = File.OpenWrite(localFilePath);
                await sourceStream.CopyToAsync(localFileStream);

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

    // Vrátí záložku na výchozí jednobarevné pozadí - hlavně pro obnovu záložek zasažených starým
    // bugem (obrázek zmizel po aktualizaci appky), kdy jediná cesta ven byla obrázek znovu vybrat.
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