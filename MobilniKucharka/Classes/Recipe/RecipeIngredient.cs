using SQLite;

namespace MobilniKucharka.Classes.Recipe
{
    public class RecipeIngredient
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int RecipeId { get; set; } // Vazba na Recipe
        public int ProductId { get; set; } // Vazba na LocalProduct (potravinu)

        // Množství přepočítané na JEDNU osobu (např. 100g rýže, 0.5ks cibule, 150g masa)
        // Díky tomu pak stačí toto číslo jednoduše vynásobit počtem lidí
        public double AmountPerPerson { get; set; }
    }
}