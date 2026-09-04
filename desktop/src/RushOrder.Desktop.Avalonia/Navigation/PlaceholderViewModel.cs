using CommunityToolkit.Mvvm.ComponentModel;

namespace RushOrder.Desktop.Avalonia.Navigation;

public sealed partial class PlaceholderViewModel : ObservableObject
{
    public string RouteKey { get; }
    public object? Parameter { get; }

    public PlaceholderViewModel(string routeKey, object? parameter)
    {
        RouteKey = routeKey;
        Parameter = parameter;
    }
}
