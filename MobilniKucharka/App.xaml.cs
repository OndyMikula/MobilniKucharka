using MobilniKucharka.Classes.UserData;
using MobilniKucharka.Services;
using MobilniKucharka.Translation;

namespace MobilniKucharka;

public partial class App : Application
{
    private static BudgetPlannerService? _database;
    public static string? PendingImportGuid { get; set; }

    public static BudgetPlannerService Database
    {
        get
        {
            if (_database == null)
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "kucharka.db3");
                _database = new BudgetPlannerService(dbPath);
            }
            return _database;
        }
    }

    [Obsolete("Parameterless constructor required by MAUI/WinUI startup - keep as-is, not meant for reuse.")]
    public App()
    {
        InitializeComponent();

        Task.Run(() => UiTranslator.InitializeAsync()).GetAwaiter().GetResult();

        // Onboarding je teď Blazor route (/onboarding) - RecipesPage.OnInitializedAsync přesměruje
        // tam sám, pokud IsOnboardingComplete ještě není nastavené. AppShell (BlazorShellPage) se
        // tedy startuje vždy, ne jen po dokončeném onboardingu.
        MainPage = new AppShell();
    }

    public static void ResetDatabase()
    {
        _database = null;
    }
}