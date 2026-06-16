using Microsoft.Extensions.DependencyInjection;

namespace RushOrder.Desktop.Navigation;

public sealed class NavigationService
{
    private readonly IServiceProvider _services;
    private readonly Stack<(UserControl View, object? Parameter)> _stack = new();

    public event Action<UserControl>? Navigated;
    public UserControl? Current => _stack.Count > 0 ? _stack.Peek().View : null;
    public bool CanGoBack => _stack.Count > 1;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public void NavigateTo<T>(object? parameter = null) where T : UserControl
    {
        var view = (T?)_services.GetService(typeof(T))
                   ?? ActivatorUtilities.CreateInstance<T>(_services);
        _stack.Push((view, parameter));
        Navigated?.Invoke(view);
    }

    public void NavigateBack()
    {
        if (!CanGoBack) return;
        _stack.Pop();
        Navigated?.Invoke(_stack.Peek().View);
    }

    public void ClearAndNavigateTo<T>(object? parameter = null) where T : UserControl
    {
        _stack.Clear();
        NavigateTo<T>(parameter);
    }

    public object? GetCurrentParameter() => _stack.Count > 0 ? _stack.Peek().Parameter : null;
}
