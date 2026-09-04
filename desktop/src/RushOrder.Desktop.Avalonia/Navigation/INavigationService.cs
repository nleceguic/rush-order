namespace RushOrder.Desktop.Avalonia.Navigation;

public interface INavigationService
{
    object? CurrentViewModel { get; }
    event Action<object?>? Navigated;

    /// <summary>Navigates to a registered route, or to <see cref="PlaceholderViewModel"/>
    /// carrying <paramref name="routeKey"/> if nothing is registered for it yet — the
    /// pattern used by DASH-01/DASH-02 for "orders/kanban", "tables/floorplan",
    /// "orders/new" and "menu/products" until those modules exist.</summary>
    void NavigateTo(string routeKey, object? parameter = null);

    /// <summary>Registers a factory for a route key. Later phases (Tables, Orders) call
    /// this to replace the placeholder with a real view model.</summary>
    void Register(string routeKey, Func<object?, object> factory);
}
