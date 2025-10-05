using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace DataField42.Converters;

public abstract class ConverterBase : MarkupExtension, IValueConverter
{
    // MarkupExtension automatically returns the same instance (singleton per converter type)
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    // Implement Convert in derived classes
    public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);

    // Optional: allow derived converters to override ConvertBack if needed
    public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException($"{GetType().Name} does not support {nameof(ConvertBack)}.");
    }
}
