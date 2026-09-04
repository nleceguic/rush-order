using RushOrder.Desktop.Avalonia.Navigation;
using Xunit;

namespace RushOrder.Desktop.Avalonia.Tests.Navigation;

public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_unknown_route_resolves_to_PlaceholderViewModel_with_the_route_key()
    {
        var nav = new NavigationService();

        nav.NavigateTo("orders/kanban");

        var placeholder = Assert.IsType<PlaceholderViewModel>(nav.CurrentViewModel);
        Assert.Equal("orders/kanban", placeholder.RouteKey);
    }

    [Fact]
    public void NavigateTo_raises_Navigated_with_the_new_view_model()
    {
        var nav = new NavigationService();
        object? raised = null;
        nav.Navigated += vm => raised = vm;

        nav.NavigateTo("tables/floorplan");

        Assert.Same(nav.CurrentViewModel, raised);
    }
}
