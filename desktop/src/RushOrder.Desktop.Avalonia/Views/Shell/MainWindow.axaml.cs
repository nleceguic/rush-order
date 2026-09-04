using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RushOrder.Desktop.Avalonia.Views.Shell;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
