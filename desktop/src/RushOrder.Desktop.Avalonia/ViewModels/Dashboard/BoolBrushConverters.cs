using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RushOrder.Desktop.Avalonia.ViewModels.Dashboard;

public static class BoolBrushConverters
{
    public static readonly IValueConverter WarningOrInfo = new FuncValueConverter<bool, IBrush>(isUrgent =>
        isUrgent
            ? (IBrush)global::Avalonia.Application.Current!.FindResource("WarningBrush")!
            : (IBrush)global::Avalonia.Application.Current!.FindResource("InfoBrush")!);
}
