using SQLite;

namespace MobilniKucharka.Classes.UserData.Bookmark
{
    public class RecipeBookmark
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}