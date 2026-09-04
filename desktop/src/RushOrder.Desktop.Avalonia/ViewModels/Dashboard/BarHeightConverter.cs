using Avalonia.Data.Converters;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class BarHeightConverter : IValueConverter
{
    public static readonly BarHeightConverter Instance = new();
    private const double MaxReference = 60; // €60 ceiling ≈ typical avg-ticket range

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var amount = value is decimal d ? (double)d : 0;
        var fraction = Math.Clamp(amount / MaxReference, 0, 1);
        return Math.Max(4, fraction * 60);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
