namespace MobilniKucharka.Classes.UserData
{
    public class UserPreferences
    {
        public int PeopleCount { get; set; } = 2; // Výchozí hodnota
        public double WeeklyBudget { get; set; } = 2000.0; //Výchozí hodnota
        public List<string> Diets { get; set; } = [];
        public List<string> Appliances { get; set; } = [];
    }
}