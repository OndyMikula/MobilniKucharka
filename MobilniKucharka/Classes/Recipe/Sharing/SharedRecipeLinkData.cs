namespace MobilniKucharka.Classes.Recipe.Sharing
{
    public class SharedRecipeLinkData
    {
        public string Name_CS { get; set; } = "";
        public string Name_EN { get; set; } = "";
        public string DescriptionText { get; set; } = "";
        public string IngredientsRaw { get; set; } = "";
        public string StepsJson_CS { get; set; } = "";
        public string StepsJson_EN { get; set; } = "";
        public string EquipmentJson { get; set; } = "";
        public string DietaryFlagsJson { get; set; } = "";
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Sugar { get; set; }
        public bool IsNutritionEstimated { get; set; }
        public double ManualCost { get; set; }
        public int PrepTime { get; set; }
        public int ServingSize { get; set; }
        public string? PhotoBase64 { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }
}