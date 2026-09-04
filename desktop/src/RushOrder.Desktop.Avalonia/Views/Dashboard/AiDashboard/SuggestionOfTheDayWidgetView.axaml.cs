using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Views.Dashboard.AiDashboard;

public partial class SuggestionOfTheDayWidgetView : UserControl
{
    public SuggestionOfTheDayWidgetView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
