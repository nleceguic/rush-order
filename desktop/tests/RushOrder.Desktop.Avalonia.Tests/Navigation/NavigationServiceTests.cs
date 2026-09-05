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

    [Fact]
    public void NavigateTo_disposes_the_outgoing_view_model_when_it_implements_IDisposable()
    {
        var nav = new NavigationService();
        var disposableVm = new DisposableFakeViewModel();
        nav.Register("current", _ => disposableVm);

        nav.NavigateTo("current");
        nav.NavigateTo("elsewhere");

        Assert.True(disposableVm.WasDisposed);
    }

    private sealed class DisposableFakeViewModel : IDisposable
    {
        public bool WasDisposed { get; private set; }
        public void Dispose() => WasDisposed = true;
    }
}
