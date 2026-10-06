using SQLite;

namespace MobilniKucharka.Classes.Shopping
{
    public class ShoppingList
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    // Položka patří k receptu (RecipeId), aby šel recept ze seznamu odebrat
    public class ShoppingListItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int ListId { get; set; }
        public int RecipeId { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;

        // Množství v jednotce produktu (pro cenu), 0 = nerozpoznané (pak AmountText)
        public double Amount { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string AmountText { get; set; } = string.Empty;

        // Množství tak, jak ho uživatel zadal (např. 50 l)
        public double DisplayAmount { get; set; }
        public string DisplayUnit { get; set; } = string.Empty;
    }

    public class ShoppingListSummary
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public double TotalPrice { get; set; }
        public bool HasUnpriced { get; set; }
    }

    public class ShoppingListEntry
    {
        public int ProductId { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AmountText { get; set; } = string.Empty;
        public double Price { get; set; }
        public bool IsUnpriced { get; set; }

        // Předvyplnění úpravy
        public double EditAmount { get; set; }
        public string EditUnit { get; set; } = string.Empty;
        public double EditPrice { get; set; }
    }

    public class ShoppingSelectionModel
    {
        public int ListId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsInList { get; set; }
    }
}