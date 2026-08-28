namespace MobilniKucharka.Classes.Legal
{
    public partial class LegalDocumentPage : ContentPage
    {
        // Generická stránka pro ToS, Privacy Policy a Third-Party Notices - obsah (nadpis + sekce)
        // se vybere podle documentType, vždy v angličtině (viz LegalContent). License má vlastní
        // stránku (LicensePage), přes tuhle už neprochází.
        public LegalDocumentPage(LegalDocumentType documentType)
        {
            InitializeComponent();
            Title = LegalContent.GetTitle(documentType);
            BindableLayout.SetItemsSource(SectionsLayout, LegalContent.GetSections(documentType));
        }
    }
}