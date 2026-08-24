namespace MobilniKucharka.Classes.Legal
{
    public partial class LicensePage : ContentPage
    {
        public LicensePage()
        {
            InitializeComponent();
            _ = LoadLicenseTextAsync();
        }

        // Plné znění Apache License 2.0 je bundlované jako Raw asset (Resources/Raw/LICENSE_apache2.txt,
        // stejný mechanismus jako ui_translations_en.json - viz UiTranslator.InitializeAsync), ne
        // zkrácený/přeformulovaný souhrn. Obsah souboru je identický s LICENSE souborem v kořeni repozitáře.
        private async Task LoadLicenseTextAsync()
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("LICENSE.txt");
                using var reader = new StreamReader(stream);
                string fullText = await reader.ReadToEndAsync();

                LicenseTextLabel.Text = fullText;
            }
            catch (Exception ex)
            {
                LicenseTextLabel.Text = $"Couldn't load the license text. You can still view it on GitHub below.\n({ex.Message})";
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
                LicenseTextLabel.IsVisible = true;
            }
        }

        private async void OnViewOnGitHubClicked(object sender, EventArgs e)
        {
            await Launcher.Default.OpenAsync(LegalContent.LicenseFileUrl);
        }
    }
}