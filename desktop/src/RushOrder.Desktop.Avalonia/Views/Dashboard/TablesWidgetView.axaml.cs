using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Views.Dashboard;

public partial class TablesWidgetView : UserControl
{
    public TablesWidgetView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
