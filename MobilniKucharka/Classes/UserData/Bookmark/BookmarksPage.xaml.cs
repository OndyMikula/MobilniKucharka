using System.Collections.ObjectModel;

namespace MobilniKucharka.Classes.UserData.Bookmark;

public partial class BookmarksPage : ContentPage
{
    public ObservableCollection<Bookmark> Bookmarks { get; set; } = [];
    private string _editingCategoryName = string.Empty;

    public BookmarksPage()
    {
        InitializeComponent();
        BookmarksCollectionView.ItemsSource = Bookmarks;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBookmarksAsync();
    }

    private async Task LoadBookmarksAsync()
    {
        var bookmarks = await App.Database.GetAllBookmarksAsync();
        Bookmarks.Clear();
        foreach (var b in bookmarks)
            Bookmarks.Add(b);
    }

    private async void OnCategoryTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string categoryName || string.IsNullOrEmpty(categoryName))
            return;

        await Navigation.PushAsync(new BookmarkCategoryPage(categoryName));
    }

    private async void OnCreateBookmarkClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new CreateBookmarkPage());
    }

    private async void OnBookmarkOptionsClicked(object sender, TappedEventArgs e)
    {
        string categoryName = e.Parameter?.ToString() ?? "";
        if (string.IsNullOrEmpty(categoryName)) return;

        bool isProtected = categoryName is "Oblíbené" or "Koncepty";
        var bookmark = Bookmarks.FirstOrDefault(b => b.Name == categoryName);
        string pinLabel = (bookmark?.IsPinned ?? false) ? "Odepnout" : "Připnout";

        string action = isProtected
            ? await DisplayActionSheet(categoryName, "Zrušit", null, "Upravit recepty ve složce", pinLabel)
            : await DisplayActionSheet(categoryName, "Zrušit", null, "Upravit recepty ve složce", pinLabel, "Smazat záložku");

        if (action == "Upravit recepty ve složce")
        {
            await OpenEditBookmarkOverlayAsync(categoryName);
        }
        else if (action == pinLabel)
        {
            await App.Database.TogglePinAsync(categoryName);
            await LoadBookmarksAsync();
        }
        else if (action == "Smazat záložku")
        {
            bool confirm = await DisplayAlert("Smazat záložku", $"Opravdu chceš smazat záložku \"{categoryName}\"? Recepty samotné zůstanou zachované.", "Smazat", "Zrušit");
            if (confirm)
            {
                await App.Database.DeleteBookmarkAsync(categoryName);
                await LoadBookmarksAsync();
            }
        }
    }

    private async Task OpenEditBookmarkOverlayAsync(string categoryName)
    {
        _editingCategoryName = categoryName;
        EditBookmarkTitleLabel.Text = categoryName;

        var allRecipes = await App.Database.GetAllRecipesAsync();
        var recipesInBookmark = await App.Database.GetRecipesByCategoryAsync(categoryName);
        var idsInBookmark = recipesInBookmark.Select(r => r.Id).ToHashSet();

        var selectionList = allRecipes.Select(r => new BookmarkedRecipeSelectionModel
        {
            RecipeId = r.Id,
            RecipeName = r.Name_CS,
            IsInBookmark = idsInBookmark.Contains(r.Id)
        }).ToList();

        BookmarkRecipesSelectionLayout.ItemsSource = selectionList;
        EditBookmarkOverlay.IsVisible = true;
    }

    private async void OnBookmarkRecipeCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (((CheckBox)sender).BindingContext is BookmarkedRecipeSelectionModel model)
        {
            if (e.Value)
                await App.Database.AddRecipeToCategoryAsync(model.RecipeId, _editingCategoryName);
            else
                await App.Database.RemoveRecipeFromCategoryAsync(model.RecipeId, _editingCategoryName);
        }
    }

    private void OnCloseEditBookmarkOverlayClicked(object sender, EventArgs e)
    {
        EditBookmarkOverlay.IsVisible = false;
    }

    private readonly ObservableCollection<BookmarkReorderItem> ReorderItems = [];

    private async void OnPageOptionsClicked(object sender, TappedEventArgs e)
    {
        string action = await DisplayActionSheet("Možnosti", "Zrušit", null, "Upravit pořadí záložek");
        if (action == "Upravit pořadí záložek")
            await OpenReorderOverlayAsync();
    }

    private Task OpenReorderOverlayAsync()
    {
        ReorderItems.Clear();
        foreach (var b in Bookmarks)
            ReorderItems.Add(new BookmarkReorderItem { Name = b.Name, IsPinned = b.IsPinned });

        ReorderCollectionView.ItemsSource = ReorderItems;
        ReorderOverlay.IsVisible = true;
        return Task.CompletedTask;
    }

    private void OnMoveBookmarkUpClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string name) return;
        int index = ReorderItems.ToList().FindIndex(i => i.Name == name);
        if (index > 0) ReorderItems.Move(index, index - 1);
    }

    private void OnMoveBookmarkDownClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string name) return;
        int index = ReorderItems.ToList().FindIndex(i => i.Name == name);
        if (index >= 0 && index < ReorderItems.Count - 1) ReorderItems.Move(index, index + 1);
    }

    private async void OnSaveReorderClicked(object sender, EventArgs e)
    {
        await App.Database.UpdateBookmarkOrderAsync([.. ReorderItems.Select(i => i.Name)]);
        ReorderOverlay.IsVisible = false;
        await LoadBookmarksAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        EditBookmarkOverlay.IsVisible = false;
        ReorderOverlay.IsVisible = false;
    }
}