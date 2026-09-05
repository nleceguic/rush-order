namespace RushOrder.Desktop.Avalonia.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<string, Func<object?, object>> _routes = new();

    public object? CurrentViewModel { get; private set; }
    public event Action<object?>? Navigated;

    public void Register(string routeKey, Func<object?, object> factory) => _routes[routeKey] = factory;

    public void NavigateTo(string routeKey, object? parameter = null)
    {
        // Dispose the outgoing view model before losing the last reference to it — DashboardViewModel/
        // AiDashboardViewModel unsubscribe their RealTimeService events and stop their refresh Timer in
        // Dispose(); without this, every navigation away from them leaks both for the app's lifetime.
        if (CurrentViewModel is IDisposable disposable)
            disposable.Dispose();

        CurrentViewModel = _routes.TryGetValue(routeKey, out var factory)
            ? factory(parameter)
            : new PlaceholderViewModel(routeKey, parameter);
        Navigated?.Invoke(CurrentViewModel);
    }
}
