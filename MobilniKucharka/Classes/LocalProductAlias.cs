using SQLite;

namespace MobilniKucharka.Classes
{
    public class LocalProductAlias
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int ProductId { get; set; }
        public string Alias { get; set; } = string.Empty;
    }
}