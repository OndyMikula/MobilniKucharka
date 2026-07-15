using SQLite;

namespace MobilniKucharka.Classes
{
    public class LocalProduct
    {
        // PrimaryKey zajišťuje rychlé vyhledávání. 
        // Bude to buď EAN (čárový kód), nebo unikátní ID z nutridatabaze.cz
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        // Nutriční hodnoty vázané standardně na 100g
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Sugar { get; set; }

        // Flag pro rozlišení původu dat při případných aktualizacích
        public bool IsFromCsv { get; set; }
    }
}