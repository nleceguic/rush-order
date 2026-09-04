using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Views.Dashboard;

public partial class ActiveOrdersWidgetView : UserControl
{
    public ActiveOrdersWidgetView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
