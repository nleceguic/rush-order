using Avalonia.Data.Converters;

namespace RushOrder.Desktop.Avalonia.ViewModels;

public static class WidgetStateConverters
{
    public static readonly IValueConverter IsLoading =
        new FuncValueConverter<WidgetLoadState, bool>(s => s == WidgetLoadState.Loading);
    public static readonly IValueConverter IsEmpty =
        new FuncValueConverter<WidgetLoadState, bool>(s => s == WidgetLoadState.Empty);
    public static readonly IValueConverter HasContent =
        new FuncValueConverter<WidgetLoadState, bool>(s => s is WidgetLoadState.Loaded or WidgetLoadState.Error);
}
