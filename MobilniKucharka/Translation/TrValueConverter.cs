using System.Globalization;

namespace MobilniKucharka.Translation
{
    // Pro bindingy, které chceš přeložit tak, jak jsou (bez šablony/placeholderu) -
    // typicky data uložená v DB, kde podkladová hodnota musí zůstat česky kvůli
    // porovnávání jinde v kódu (např. Bookmark.Name), ale zobrazení má být přeložené.
    // Použití: Text="{Binding Name, Converter={StaticResource TrValueConverter}}"
    public class TrValueConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            UiTranslator.Tr(value?.ToString() ?? string.Empty);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}