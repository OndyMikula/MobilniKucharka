using MobilniKucharka.Services;

namespace MobilniKucharka;

public partial class App : Application
{
    private static BudgetPlannerService? _database;

    // Tato statická vlastnost zaručí, že kdekoli v aplikaci napíšeš "App.Database",
    // dostaneš připravenou instanci tvé hlavní služby BudgetPlannerService.
    public static BudgetPlannerService Database
    {
        get
        {
            if (_database == null)
            {
                // Vytvoříme cestu k databázovému souboru
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "kucharka.db3");
                _database = new BudgetPlannerService(dbPath);
            }
            return _database;
        }
    }

    [Obsolete]
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell(); // případně tvoje startovní stránka
    }
}