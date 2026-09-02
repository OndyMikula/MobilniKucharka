using MobilniKucharka.Services;
using MobilniKucharka.Translation;

namespace MobilniKucharka;

public partial class App : Application
{
    private static BudgetPlannerService? _database;

    public static string? PendingImportGuid { get; set; }
    public static string? PendingBlazorRoute { get; set; }

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

        MainPage = new AppShell();
    }

    public static void ResetDatabase()
    {
        _database = null;
    }
}