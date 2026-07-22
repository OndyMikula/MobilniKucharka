using SQLite;

namespace MobilniKucharka.Classes
{
    public class LocalProduct
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name_CS { get; set; } = string.Empty;
        public string Name_EN { get; set; } = string.Empty;

        // Ceny v Kč (pro přepočet budgetu)
        public double PriceKaufland { get; set; }
        public double PriceLidl { get; set; }
        public double PricePenny { get; set; }
        public double PriceTesco { get; set; }
        public double PriceBilla { get; set; }
        public double PriceAlbert { get; set; }

        public string Unit { get; set; } = "g"; // g, ml, ks

        // Dietní filtry
        public bool IsVegetarian { get; set; }
        public bool IsVegan { get; set; }
        public bool IsLactoseFree { get; set; }
    }
}