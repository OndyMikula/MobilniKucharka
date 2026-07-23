using SQLite;

namespace MobilniKucharka.Classes.UserData.Bookmark
{
    public class Bookmark
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Buď barva v HEX formátu (např. #2196F3), nebo cesta k obrázku na pozadí
        public string BackgroundColor { get; set; } = "#2196F3";
        public string BackgroundImage { get; set; } = string.Empty;
        public string Icon { get; set; } = "📁";

        public bool UseImageAsBackground => !string.IsNullOrEmpty(BackgroundImage);
        public bool ShowIcon => !UseImageAsBackground;

        //Bookmark order
        public bool IsPinned { get; set; }
        public int SortOrder { get; set; }
        public DateTime LastEditedUtc { get; set; } = DateTime.UtcNow;
        public bool HasManualOrder { get; set; }
    }
}