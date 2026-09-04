using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public sealed class DeltaColorConverter : IValueConverter
{
    public static readonly DeltaColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is true ? global::Avalonia.Application.Current!.FindResource("SuccessBrush")! : global::Avalonia.Application.Current!.FindResource("ErrorBrush")!;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
