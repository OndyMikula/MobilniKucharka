using SQLite;

namespace MobilniKucharka.Classes
{
    public class LocalProduct
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name_CS { get; set; } = string.Empty;
        public string Name_EN { get; set; } = string.Empty;

        // Průměrná cena (zdroj: ČSÚ, případně počáteční odhad)
        public double PriceAverage { get; set; }

        // Uživatel může cenu přebít vlastní hodnotou (např. z konkrétního obchodu)
        public bool HasManualPrice { get; set; }
        public double ManualPrice { get; set; }

        public string Unit { get; set; } = "g";

        public bool IsVegetarian { get; set; }
        public bool IsVegan { get; set; }
        public bool IsLactoseFree { get; set; }

        [Ignore]
        public double EffectivePrice => HasManualPrice ? ManualPrice : PriceAverage;
    }
}