namespace RushOrder.Desktop.State;

public sealed class AppState
{
    public UserInfo? CurrentUser { get; private set; }
    public RestaurantInfo? CurrentRestaurant { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public bool IsOnline { get; private set; }

    public event Action<bool>? OnlineStatusChanged;
    public event Action<UserInfo?>? UserChanged;
    public event Action<RestaurantInfo?>? RestaurantChanged;

    public bool IsAuthenticated => CurrentUser is not null;

    public void SetAuthenticated(UserInfo user, string accessToken, string refreshToken)
    {
        CurrentUser = user;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        UserChanged?.Invoke(user);
    }

    public void SetRestaurant(RestaurantInfo restaurant)
    {
        CurrentRestaurant = restaurant;
        RestaurantChanged?.Invoke(restaurant);
    }

    public void SetOnlineStatus(bool isOnline)
    {
        if (IsOnline == isOnline) return;
        IsOnline = isOnline;
        OnlineStatusChanged?.Invoke(isOnline);
    }

    public void Logout()
    {
        CurrentUser = null;
        AccessToken = null;
        RefreshToken = null;
        CurrentRestaurant = null;
        UserChanged?.Invoke(null);
    }
}

public sealed record UserInfo(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    string AvatarInitials);

public sealed record RestaurantInfo(
    Guid Id,
    string Name,
    string? LogoUrl);
