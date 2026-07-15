namespace MobilniKucharka.Classes
{
    public class Recipe
    {
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;

        // Nutriční hodnoty
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Sugar { get; set; }

        public List<string> Steps { get; set; } = new List<string>();
        public string SourceUrl { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = new List<string>();
        public List<string> Tools { get; set; } = new List<string>();
    }
}