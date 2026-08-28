namespace MobilniKucharka.Translation
{
    // XAML: Text="{loc:Tr 'Rozepsaný recept'}"
    // Stránka se při přepnutí jazyka celá znovu sestavuje (viz SettingsPage.OnLanguageChanged),
    // takže stačí vyhodnotit překlad jednou při konstrukci - není potřeba živé rebindování.
    [ContentProperty(nameof(Text))]
    [AcceptEmptyServiceProvider]
    public class TrExtension : IMarkupExtension<string>
    {
        public string Text { get; set; } = string.Empty;

        public string ProvideValue(IServiceProvider serviceProvider) => UiTranslator.Tr(Text);

        object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
    }
}