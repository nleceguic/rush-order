namespace RushOrder.Desktop.Avalonia.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<string, Func<object?, object>> _routes = new();

    public object? CurrentViewModel { get; private set; }
    public event Action<object?>? Navigated;

    public void Register(string routeKey, Func<object?, object> factory) => _routes[routeKey] = factory;

    public void NavigateTo(string routeKey, object? parameter = null)
    {
        CurrentViewModel = _routes.TryGetValue(routeKey, out var factory)
            ? factory(parameter)
            : new PlaceholderViewModel(routeKey, parameter);
        Navigated?.Invoke(CurrentViewModel);
    }
}
