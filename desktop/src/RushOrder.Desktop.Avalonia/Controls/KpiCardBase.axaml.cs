using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Reactive;
using RushOrder.Desktop.Avalonia.Animations;
using RushOrder.Desktop.Avalonia.ViewModels;
using System.Windows.Input;

namespace RushOrder.Desktop.Avalonia.Controls;

public partial class KpiCardBase : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<KpiCardBase, string>(nameof(Title));
    public static readonly StyledProperty<WidgetLoadState> StateProperty =
        AvaloniaProperty.Register<KpiCardBase, WidgetLoadState>(nameof(State));
    public static readonly StyledProperty<bool> IsShowingSimulatedDataProperty =
        AvaloniaProperty.Register<KpiCardBase, bool>(nameof(IsShowingSimulatedData));
    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<KpiCardBase, ICommand?>(nameof(RetryCommand));
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<KpiCardBase, object?>(nameof(Content));

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public WidgetLoadState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public bool IsShowingSimulatedData { get => GetValue(IsShowingSimulatedDataProperty); set => SetValue(IsShowingSimulatedDataProperty, value); }
    public ICommand? RetryCommand { get => GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    public KpiCardBase()
    {
        InitializeComponent();
        this.GetObservable(StateProperty).Subscribe(new AnonymousObserver<WidgetLoadState>(_ =>
        {
            if (State == WidgetLoadState.Loaded) RefreshPulse.Play(this);
        }));
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
