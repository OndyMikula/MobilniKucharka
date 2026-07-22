using Microsoft.Maui;
using MobilniKucharka.Classes.UserData;

namespace MobilniKucharka
{
    public partial class OnboardingPage : ContentPage
    {
        private int _currentStep = 1;
        private readonly int _totalSteps = 4;
        private UserPreferences _preferences = new();

        public OnboardingPage()
        {
            InitializeComponent();
            UpdateStepUI();
        }

        private void OnShopSelected(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                // Uložíme název sloupce v DB (např. PriceLidl, PriceKaufland)
                _preferences.PreferredShop = button.CommandParameter?.ToString() ?? "PriceLidl";
                SelectedShopLabel.Text = $"Zvolený obchod: {button.Text}";
            }
        }

        private void OnPeopleChanged(object sender, ValueChangedEventArgs e)
        {
            int people = (int)e.NewValue;
            _preferences.PeopleCount = people;

            if (people == 1) PeopleLabel.Text = "1 osoba";
            else if (people > 1 && people < 5) PeopleLabel.Text = $"{people} lidé";
            else PeopleLabel.Text = $"{people} lidí";
        }

        private void OnBudgetChanged(object sender, ValueChangedEventArgs e)
        {
            // Zaokrouhlování na celé padesátikoruny pro čistší UI
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

        private async void OnNextClicked(object sender, EventArgs e)
        {
            // Validace před postupem dál
            if (_currentStep == 1 && string.IsNullOrEmpty(_preferences.PreferredShop))
            {
                await DisplayAlert("Výběr", "Vyber prosím jeden ze supermarketů kliknutím na něj.", "Rozumím");
                return;
            }

            if (_currentStep < _totalSteps)
            {
                _currentStep++;
                UpdateStepUI();
            }
            else
            {
                // Dokončení onboarding formuláře
                SaveFinalData();

                // Uložíme informaci, že onboarding byl úspěšně vyplněn
                Preferences.Default.Set("IsOnboardingComplete", true);

                // Přechod na hlavní obrazovku
                Application.Current!.Windows[0].Page = new NavigationPage(new MainPage());
            }
        }

        private void UpdateStepUI()
        {
            // Skrytí všech sekcí
            Step1_Shop.IsVisible = false;
            Step2_Budget.IsVisible = false;
            Step3_Diets.IsVisible = false;
            Step4_Appliances.IsVisible = false;

            // Zobrazení tlačítka zpět
            BackButton.IsVisible = _currentStep > 1;

            // Nastavení textu hlavního tlačítka
            NextButton.Text = _currentStep == _totalSteps ? "Vygenerovat jídelníček" : "Pokračovat";

            // Nastavení progress baru
            WizardProgress.Progress = (double)_currentStep / _totalSteps;

            // Zobrazení odpovídajícího kroku
            switch (_currentStep)
            {
                case 1:
                    StepTitle.Text = "Kde nakupuješ?";
                    StepDescription.Text = "Přizpůsobíme nákupní košík cenám tvého oblíbeného obchodu.";
                    Step1_Shop.IsVisible = true;
                    break;
                case 2:
                    StepTitle.Text = "Počet lidí a rozpočet";
                    StepDescription.Text = "Nastav, kolik krků budeš krmit a kolik peněz chceš utratit.";
                    Step2_Budget.IsVisible = true;
                    break;
                case 3:
                    StepTitle.Text = "Stravovací návyky";
                    StepDescription.Text = "Omezíme recepty, které nevyhovují tvým potřebám.";
                    Step3_Diets.IsVisible = true;
                    break;
                case 4:
                    StepTitle.Text = "Co máš v kuchyni?";
                    StepDescription.Text = "Nebudeme ti navrhovat pečení v troubě, pokud máš jen mikrovlnku.";
                    Step4_Appliances.IsVisible = true;
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

            // Uložíme preference do lokální cache (Preferences), aby k nim měla přístup hlavní stránka
            Preferences.Default.Set("ShopColName", _preferences.PreferredShop);
            Preferences.Default.Set("PeopleCount", _preferences.PeopleCount);
            Preferences.Default.Set("WeeklyBudget", _preferences.WeeklyBudget);

            // Seznamy uložíme jako čárkou oddělený string
            Preferences.Default.Set("UserDiets", string.Join(",", _preferences.Diets));
            Preferences.Default.Set("UserAppliances", string.Join(",", _preferences.Appliances));
        }
    }
}