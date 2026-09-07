using MobilniKucharka.Classes.UserData;
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

        // OnboardingPage se dřív dala zobrazit jen ručně přes Settings ("Změnit") - appka nikdy
        // nekontrolovala, jestli onboarding vůbec proběhl, takže po instalaci šla appka rovnou do
        // AppShell/Blazor shellu se seedovanými ukázkovými daty a výchozími preferencemi, bez
        // jakéhokoli prvotního nastavení. "IsOnboardingComplete" se nastavuje až na konci
        // OnboardingPage.OnNextClicked (nebo po obnově ze zálohy - viz CompleteOnboardingAndEnterApp
        // tam), takže dokud appka poprvé neprojde jednou z těch dvou cest, MainPage zůstává
        // OnboardingPage při každém studeném startu.
        bool isOnboardingComplete = Preferences.Default.Get("IsOnboardingComplete", false);
        MainPage = isOnboardingComplete ? new AppShell() : new OnboardingPage();
    }

    public static void ResetDatabase()
    {
        _database = null;
    }
}