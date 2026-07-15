namespace MobilniKucharka
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // ZDE UŽ SE NENASTAVUJE MainPage = ...
        }

        // NOVÝ ZPŮSOB V .NET 9 PRO URČENÍ START Z OBRAZOVKY
        protected override Window CreateWindow(IActivationState? activationState)
        {
            bool isOnboardingComplete = Preferences.Default.Get("IsOnboardingComplete", false);

            Page initialPage;
            if (isOnboardingComplete)
            {
                initialPage = new NavigationPage(new MainPage());
            }
            else
            {
                initialPage = new OnboardingPage();
            }

            return new Window(initialPage);
        }
    }
}