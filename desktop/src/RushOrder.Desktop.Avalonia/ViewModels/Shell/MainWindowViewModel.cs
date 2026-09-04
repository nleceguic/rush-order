using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RushOrder.Desktop.Avalonia.Navigation;

namespace RushOrder.Desktop.Avalonia.ViewModels.Shell;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _nav;

    public IReadOnlyList<NavItem> NavItems { get; } =
    [
        new("dashboard",   "⊞", "Dashboard"),
        new("tables",      "◻", "Mesas"),
        new("orders",      "▤", "Pedidos"),
        new("kitchen",     "≡", "Cocina"),
        new("menu",        "◈", "Menú"),
        new("waiters",     "♟", "Camareros"),
        new("reservas",    "◻", "Reservas"),
        new("statistics",  "☰", "Estadísticas"),
        new("panel-ia",    "✦", "Panel IA"),
        new("billing",     "$", "Facturación"),
    ];

    [ObservableProperty]
    private string _activeRouteKey = "dashboard";

    public object? CurrentContent => _nav.CurrentViewModel;

    public MainWindowViewModel(INavigationService nav)
    {
        _nav = nav;
        _nav.Navigated += _ => OnPropertyChanged(nameof(CurrentContent));
        _nav.NavigateTo(ActiveRouteKey);
    }

    [RelayCommand]
    private void SelectNav(NavItem item)
    {
        ActiveRouteKey = item.RouteKey;
        _nav.NavigateTo(item.RouteKey);
    }
}
