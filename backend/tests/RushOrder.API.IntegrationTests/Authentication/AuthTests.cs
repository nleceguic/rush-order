using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RushOrder.API.IntegrationTests.Infrastructure;

namespace RushOrder.API.IntegrationTests.Authentication;

public sealed class AuthTests : IntegrationTestBase
{
    public AuthTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokens()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email    = TestConstants.OwnerEmail,
            password = TestConstants.DemoPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns422()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email    = TestConstants.OwnerEmail,
            password = "WrongPassword99!"
        });

        // BusinessRuleException is mapped to 422 by GlobalExceptionHandler
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns422()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email    = "nobody@unknown.com",
            password = "SomePass1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_WithInvalidPayload_Returns400()
    {
        var client = Factory.CreateClient();

        // Missing password — FluentValidation should reject
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "not-an-email"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokenPair()
    {
        var (_, _, refreshToken) = await Auth.LoginAsync(TestConstants.OwnerEmail, TestConstants.DemoPassword);

        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();

        var newRefreshToken = body.GetProperty("refreshToken").GetString();
        newRefreshToken.Should().NotBeNullOrEmpty();
        newRefreshToken.Should().NotBe(refreshToken, "a new refresh token must be issued on each use");
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_Returns422()
    {
        var (authClient, accessToken, refreshToken) = await Auth.LoginAsync(
            TestConstants.OwnerEmail, TestConstants.DemoPassword);

        // Logout to revoke the refresh token
        await authClient.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken });

        // Attempt to use the now-revoked refresh token
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedToken_InvalidatesTokenFamily()
    {
        var (_, _, refreshToken1) = await Auth.LoginAsync(
            TestConstants.OwnerEmail, TestConstants.DemoPassword);

        // Rotate once to get token2
        var client = Factory.CreateClient();
        var rotateResp = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refreshToken1 });
        rotateResp.EnsureSuccessStatusCode();
        var body = await rotateResp.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken2 = body.GetProperty("refreshToken").GetString()!;

        // Try to reuse the already-consumed token1 — the entire family must be invalidated
        var reuseResp = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refreshToken1 });
        reuseResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // token2 should also be invalidated (family invalidation)
        var useToken2Resp = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refreshToken2 });
        useToken2Resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AdminLogin_WithAdminCredentials_Returns200()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email    = TestConstants.AdminEmail,
            password = TestConstants.AdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }
}
