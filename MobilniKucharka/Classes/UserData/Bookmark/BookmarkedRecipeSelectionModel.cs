namespace MobilniKucharka.Classes.UserData.Bookmark
{
    public class BookmarkedRecipeSelectionModel
    {
        public int RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public bool IsInBookmark { get; set; }
    }
}