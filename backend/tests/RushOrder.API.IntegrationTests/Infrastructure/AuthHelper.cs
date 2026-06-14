using System.Net.Http.Json;
using System.Text.Json;

namespace RushOrder.API.IntegrationTests.Infrastructure;

public sealed class AuthHelper
{
    private readonly ApiFactory _factory;

    public AuthHelper(ApiFactory factory) => _factory = factory;

    public async Task<HttpClient> GetAdminClientAsync()
        => await CreateAuthenticatedClientAsync(TestConstants.AdminEmail, TestConstants.AdminPassword);

    public async Task<HttpClient> GetOwnerClientAsync()
        => await CreateAuthenticatedClientAsync(TestConstants.OwnerEmail, TestConstants.DemoPassword);

    public async Task<HttpClient> GetWaiterClientAsync()
        => await CreateAuthenticatedClientAsync(TestConstants.WaiterEmail, TestConstants.DemoPassword);

    public async Task<HttpClient> GetKitchenClientAsync()
        => await CreateAuthenticatedClientAsync(TestConstants.KitchenEmail, TestConstants.DemoPassword);

    public async Task<(HttpClient Client, string AccessToken, string RefreshToken)> LoginAsync(
        string email, string password)
    {
        var anonClient = _factory.CreateClient();
        var loginResp = await anonClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });

        loginResp.EnsureSuccessStatusCode();

        var json = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken  = json.GetProperty("accessToken").GetString()!;
        var refreshToken = json.GetProperty("refreshToken").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        return (client, accessToken, refreshToken);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var (client, _, _) = await LoginAsync(email, password);
        return client;
    }
}
