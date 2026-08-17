using MobilniKucharka.Services;
using MobilniKucharka.Translation;

namespace MobilniKucharka;

public partial class App : Application
{
    private static BudgetPlannerService? _database;
    public static string? PendingImportGuid { get; set; }

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

    [Obsolete("Parameterless constructor required by MAUI/WinUI startup - keep as-is, not meant for reuse.")]
    public App()
    {
        InitializeComponent();

        // Blokujeme startup thread, dokud se nenačtou UI překlady (chceme je hotové před prvním PageRenderem) -
        // ALE musí to běžet na threadpoolu, ne přímo tady. MAUI má na UI threadu SynchronizationContext,
        // takže .GetAwaiter().GetResult() přímo na Tasku z InitializeAsync() by čekal na pokračování,
        // které se snaží vrátit na tenhle stejný, právě zablokovaný UI thread -> jistý deadlock a ANR.
        // Task.Run to spustí bez zachyceného kontextu, takže se nikdy nesnaží vrátit zpátky na UI thread.
        Task.Run(() => UiTranslator.InitializeAsync()).GetAwaiter().GetResult();

        MainPage = new AppShell();
    }

    public static void ResetDatabase()
    {
        _database = null;
    }
}
