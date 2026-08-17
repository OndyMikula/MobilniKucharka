namespace MobilniKucharka.Classes.UserData
{
    public partial class OnboardingPage : ContentPage
    {
        private int _currentStep = 1;
        private readonly int _totalSteps = 3;
        private readonly UserPreferences _preferences = new();

        public OnboardingPage()
        {
            InitializeComponent();
            UpdateStepUI();
        }

        private static string Tr(string csText) => MobilniKucharka.Translation.UiTranslator.Tr(csText);

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

        private void OnBackClicked(object sender, EventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepUI();
            }
        }

        private void OnNextClicked(object sender, EventArgs e)
        {
            if (_currentStep < _totalSteps)
            {
                _currentStep++;
                UpdateStepUI();
            }
            else
            {
                SaveFinalData();
                Preferences.Default.Set("IsOnboardingComplete", true);
                Application.Current!.Windows[0].Page = new NavigationPage(new MainPage());
            }
        }

        private void UpdateStepUI()
        {
            Step1_Budget.IsVisible = false;
            Step2_Diets.IsVisible = false;
            Step3_Appliances.IsVisible = false;

            BackButton.IsVisible = _currentStep > 1;
            NextButton.Text = _currentStep == _totalSteps ? Tr("Vygenerovat jídelníček") : Tr("Pokračovat");
            WizardProgress.Progress = (double)_currentStep / _totalSteps;

            switch (_currentStep)
            {
                case 1:
                    StepTitle.Text = Tr("Počet lidí a rozpočet");
                    StepDescription.Text = Tr("Nastav, kolik lidí budeš krmit a kolik peněz chceš utratit.");
                    Step1_Budget.IsVisible = true;
                    break;
                case 2:
                    StepTitle.Text = Tr("Stravovací návyky");
                    StepDescription.Text = Tr("Omezíme recepty, které nevyhovují tvým potřebám.");
                    Step2_Diets.IsVisible = true;
                    break;
                case 3:
                    StepTitle.Text = Tr("Co máš v kuchyni?");
                    StepDescription.Text = Tr("Nebudeme ti navrhovat pečení v troubě, pokud máš jen mikrovlnku.");
                    Step3_Appliances.IsVisible = true;
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