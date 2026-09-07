using MobilniKucharka.Services;
using System.Globalization;

namespace MobilniKucharka.Classes.UserData
{
    public partial class OnboardingPage : ContentPage
    {
        private enum Step { Language, Backup, Budget, Diets, Appliances }

        private readonly List<Step> _steps;
        private int _currentStepIndex;
        private readonly UserPreferences _preferences = new();
        private readonly bool _isFirstLaunch;

        private static string Tr(string csText) => MobilniKucharka.Translation.UiTranslator.Tr(csText);

        private static readonly FilePickerFileType BackupZipFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new[] { "application/zip", "application/x-zip-compressed" } }
        });

        public OnboardingPage()
        {
            InitializeComponent();

            _isFirstLaunch = !Preferences.Default.Get("IsOnboardingComplete", false);

            _steps = _isFirstLaunch
                ? [Step.Language, Step.Backup, Step.Budget, Step.Diets, Step.Appliances]
                : [Step.Budget, Step.Diets, Step.Appliances];

            _preferences.WeeklyBudget = BudgetSlider.Value;
            _preferences.PeopleCount = (int)PeopleStepper.Value;

            // Na rozdíl od zbytku aplikace (kde jazyk mění SettingsPage.OnLanguageChanged tím, že
            // celou appku restartuje - viz komentář v TrExtension.cs "stačí vyhodnotit překlad
            // jednou při konstrukci") se tahle stránka NErestartuje po volbě jazyka na Kroku Jazyk -
            // jazyk se vybírá přímo uvnitř už zkonstruované instance. Proto tu žádný text NESMÍ
            // jít přes statický XAML {loc:Tr '...'} (ten by zůstal navždy zamrzlý v jazyce
            // platném při InitializeComponent(), tedy v defaultní češtině) - všechny popisky se
            // nastavují tady, v RefreshLocalizedTexts(), volané jednou při startu a znovu ihned po
            // volbě jazyka (viz ApplyLanguageChoice).
            RefreshLocalizedTexts();

            UpdateStepUI();
        }

        private void RefreshLocalizedTexts()
        {
            ContinueWithoutBackupButton.Text = Tr("Nemám zálohu - pokračovat");
            LoadBackupButton.Text = Tr("Načíst data ze zálohy");

            PeopleQuestionLabel.Text = Tr("Pro kolik lidí bude nákup?");
            PeopleLabel.Text = MobilniKucharka.Translation.UiTranslator.TrPeopleCount(_preferences.PeopleCount);
            BudgetQuestionLabel.Text = Tr("Tvůj týdenní budget na suroviny:");
            BudgetLabel.Text = $"{_preferences.WeeklyBudget:N0} Kč";

            DietsQuestionLabel.Text = Tr("Vyber své stravovací preference:");
            VegetarianLabel.Text = Tr("Vegetarián");
            VeganLabel.Text = Tr("Vegan");
            LactoseLabel.Text = Tr("Bezlaktózová dieta");

            AppliancesQuestionLabel.Text = Tr("Jaké spotřebiče máš k dispozici?");
            OvenLabel.Text = Tr("Trouba");
            StoveLabel.Text = Tr("Sporák / Varná deska");
            KettleLabel.Text = Tr("Rychlovarná konvice");
            MicrowaveLabel.Text = Tr("Mikrovlnná trouba");

            BackButton.Text = Tr("Zpět");

            // NextButton se navíc přepisuje i v UpdateStepUI() podle aktuálního kroku
            // ("Pokračovat" vs. "Vygenerovat jídelníček") - tahle hodnota tu je jen bezpečný
            // výchozí stav, než UpdateStepUI() poprvé proběhne.
            NextButton.Text = Tr("Pokračovat");
        }

        private void OnPeopleChanged(object sender, ValueChangedEventArgs e)
        {
            int people = (int)e.NewValue;
            _preferences.PeopleCount = people;

            PeopleLabel.Text = MobilniKucharka.Translation.UiTranslator.TrPeopleCount(people);
        }

        private void OnBudgetChanged(object sender, ValueChangedEventArgs e)
        {
            double rounded = Math.Round(e.NewValue / 50.0) * 50;
            BudgetSlider.Value = rounded;
            _preferences.WeeklyBudget = rounded;
            BudgetLabel.Text = $"{rounded:N0} Kč";
        }

        private void OnLanguageCzechClicked(object sender, EventArgs e) => ApplyLanguageChoice("cs", "Čeština");
        private void OnLanguageEnglishClicked(object sender, EventArgs e) => ApplyLanguageChoice("en", "English");

        private void ApplyLanguageChoice(string code, string displayName)
        {
            Preferences.Default.Set("AppLanguageCode", code);
            Preferences.Default.Set("AppLanguageName", displayName);

            var culture = new CultureInfo(code);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Znovu vyhodnotí VŠECHNY statické popisky zbytku wizardu (Budget/Diety/Spotřebiče/
            // tlačítka) hned po uložení zvoleného jazyka - bez tohohle by zůstaly zamrzlé v jazyce
            // platném při InitializeComponent(), přesně to je popsaný bug.
            RefreshLocalizedTexts();

            AdvanceToNextStep();
        }

        private void OnBackClicked(object sender, EventArgs e)
        {
            if (_currentStepIndex > 0)
            {
                _currentStepIndex--;
                UpdateStepUI();
            }
        }

        private void OnNextClicked(object sender, EventArgs e) => AdvanceToNextStep();

        private void AdvanceToNextStep()
        {
            if (_currentStepIndex < _steps.Count - 1)
            {
                _currentStepIndex++;
                UpdateStepUI();
            }
            else
            {
                SaveFinalData();
                CompleteOnboardingAndEnterApp();
            }
        }

        private void OnContinueWithoutBackupClicked(object sender, EventArgs e) => AdvanceToNextStep();

        private async void OnLoadBackupFromOnboardingClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = Tr("Vyber soubor zálohy (.zip)"),
                    FileTypes = BackupZipFileType
                });
                if (result == null) return;

                BackupProgressOverlay.IsVisible = true;
                BackupProgressLabel.Text = Tr("Načítám data...");

                string localCopyPath = Path.Combine(FileSystem.CacheDirectory, $"import_{Guid.NewGuid()}.zip");
                using (var sourceStream = await result.OpenReadAsync())
                using (var localStream = File.Create(localCopyPath))
                {
                    await sourceStream.CopyToAsync(localStream);
                }

                var progress = new Progress<double>(value =>
                {
                    BackupProgressBar.Progress = value;
                    BackupProgressPercentLabel.Text = $"{value:P0}";
                });

                await DataBackupService.ImportAsync(localCopyPath, progress);

                File.Delete(localCopyPath);

                BackupProgressOverlay.IsVisible = false;

                await DisplayAlertAsync(Tr("Hotovo"), Tr("Data byla načtena. Aplikace se nyní restartuje."), "OK");
                CompleteOnboardingAndEnterApp();
            }
            catch (Exception ex)
            {
                BackupProgressOverlay.IsVisible = false;
                await DisplayAlertAsync(Tr("Chyba"), $"{Tr("Načtení se nepodařilo")}: {ex.Message}", "OK");
            }
        }

        private static void CompleteOnboardingAndEnterApp()
        {
            Preferences.Default.Set("IsOnboardingComplete", true);
            App.ResetDatabase();
            Application.Current!.Windows[0].Page = new AppShell();
        }

        private void UpdateStepUI()
        {
            Step_Language.IsVisible = false;
            Step_Backup.IsVisible = false;
            Step_Budget.IsVisible = false;
            Step_Diets.IsVisible = false;
            Step_Appliances.IsVisible = false;

            var currentStep = _steps[_currentStepIndex];

            bool showBottomNav = currentStep is Step.Budget or Step.Diets or Step.Appliances;
            BottomNavGrid.IsVisible = showBottomNav;
            WizardProgress.IsVisible = showBottomNav;

            if (showBottomNav)
            {
                BackButton.IsVisible = _currentStepIndex > 0;
                NextButton.Text = _currentStepIndex == _steps.Count - 1 ? Tr("Vygenerovat jídelníček") : Tr("Pokračovat");
                WizardProgress.Progress = (double)(_currentStepIndex + 1) / _steps.Count;
            }

            switch (currentStep)
            {
                case Step.Language:
                    Step_Language.IsVisible = true;
                    StepTitle.Text = "Vítej v Mobilní Kuchařce! / Welcome to Mobilní Kuchařka!";
                    StepDescription.Text = "Vyber jazyk aplikace. / Choose the app language.";
                    break;
                case Step.Backup:
                    Step_Backup.IsVisible = true;
                    StepTitle.Text = Tr("Vítej v Mobilní Kuchařce");
                    StepDescription.Text = Tr("Než začneš, můžeš obnovit svá data ze zálohy, nebo začít úplně od začátku.");
                    break;
                case Step.Budget:
                    Step_Budget.IsVisible = true;
                    StepTitle.Text = Tr("Počet lidí a rozpočet");
                    StepDescription.Text = Tr("Nastav, kolik lidí budeš krmit a kolik peněz chceš utratit.");
                    break;
                case Step.Diets:
                    Step_Diets.IsVisible = true;
                    StepTitle.Text = Tr("Stravovací návyky");
                    StepDescription.Text = Tr("Omezíme recepty, které nevyhovují tvým potřebám.");
                    break;
                case Step.Appliances:
                    Step_Appliances.IsVisible = true;
                    StepTitle.Text = Tr("Co máš v kuchyni?");
                    StepDescription.Text = Tr("Nebudeme ti navrhovat pečení v troubě, pokud máš jen mikrovlnku.");
                    break;
            }
        }

        private void SaveFinalData()
        {
            _preferences.Diets.Clear();
            if (CheckVegetarian.IsChecked) _preferences.Diets.Add("Vegetarian");
            if (CheckVegan.IsChecked) _preferences.Diets.Add("Vegan");
            if (CheckLactose.IsChecked) _preferences.Diets.Add("LactoseFree");

            _preferences.Appliances.Clear();
            if (CheckOven.IsChecked) _preferences.Appliances.Add("Trouba");
            if (CheckStove.IsChecked) _preferences.Appliances.Add("Sporák");
            if (CheckKettle.IsChecked) _preferences.Appliances.Add("Konvice");
            if (CheckMicrowave.IsChecked) _preferences.Appliances.Add("Mikrovlnka");

            Preferences.Default.Set("PeopleCount", _preferences.PeopleCount);
            Preferences.Default.Set("WeeklyBudget", _preferences.WeeklyBudget);
            Preferences.Default.Set("UserDiets", string.Join(",", _preferences.Diets));
            Preferences.Default.Set("UserAppliances", string.Join(",", _preferences.Appliances));
        }
    }
}