using System.Globalization;

namespace MobilniKucharka.Translation
{
    // Pro StringFormat-style bindingy, kde formátovací šablona (např. "Čas: {0} min") potřebuje
    // projít Tr() - {loc:Tr} na to nejde použít, protože StringFormat není samostatná Text vlastnost.
    // Použití: Text="{Binding Foo, Converter={StaticResource TrTemplateConverter}, ConverterParameter='Čas: {0} min'}"
    public class TrTemplateConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string template = UiTranslator.Tr(parameter?.ToString() ?? "{0}");
            return string.Format(culture, template, value);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
