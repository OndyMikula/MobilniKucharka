namespace MobilniKucharka.Classes.Legal
{
    // Jedna sekce právního dokumentu (nadpis + text) - zobrazovaná v LegalDocumentPage.
    public class LegalSection
    {
        public string Heading { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}