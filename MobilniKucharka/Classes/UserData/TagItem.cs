using SQLite;

namespace MobilniKucharka.Classes.UserData
{
    public class TagItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Atributy: "Ingredient", "Equipment" nebo "Both" (pokud uživatel nezvolí kategorii)
        public string Category { get; set; } = "Both";
    }
}