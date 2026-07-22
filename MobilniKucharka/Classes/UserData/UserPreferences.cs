namespace MobilniKucharka.Classes.UserData
{
    public class UserPreferences
    {
        public string PreferredShop { get; set; } = string.Empty;
        public int PeopleCount { get; set; } = 2; // Výchozí hodnota
        public double WeeklyBudget { get; set; } = 1500.0;
        public List<string> Diets { get; set; } = [];
        public List<string> Appliances { get; set; } = [];
    }
}