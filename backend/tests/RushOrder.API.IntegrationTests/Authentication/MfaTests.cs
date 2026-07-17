using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RushOrder.API.IntegrationTests.Infrastructure;

namespace RushOrder.API.IntegrationTests.Authentication;

public sealed class MfaTests : IntegrationTestBase
{
    public MfaTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task MfaVerify_WithValidCode_ReturnsTokens()
    {
        // ── Step 1: Login and get an authenticated client ────────────────────
        var (ownerClient, _, _) = await Auth.LoginAsync(
            TestConstants.OwnerEmail, TestConstants.DemoPassword);

        // ── Step 2: Initiate MFA setup ───────────────────────────────────────
        var setupResp = await ownerClient.PostAsync("/api/v1/auth/mfa/setup", null);
        setupResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var setupBody = await setupResp.Content.ReadFromJsonAsync<JsonElement>();
        setupBody.GetProperty("secret").GetString().Should().Be(FixedCodeTotpService.TestSecret,
            "FixedCodeTotpService always returns the deterministic test secret");

        // ── Step 3: Confirm MFA with the fixed code ──────────────────────────
        var confirmResp = await ownerClient.PostAsJsonAsync("/api/v1/auth/mfa/confirm", new
        {
            code = FixedCodeTotpService.ValidCode
        });
        confirmResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "MFA should be confirmed with the test code");

        // ── Step 4: Login again — should require MFA now ─────────────────────
        var anonClient = Factory.CreateClient();
        var loginResp = await anonClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email    = TestConstants.OwnerEmail,
            password = TestConstants.DemoPassword
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        loginBody.GetProperty("requiresMfa").GetBoolean().Should().BeTrue();
        var tempToken = loginBody.GetProperty("tempToken").GetString();
        tempToken.Should().NotBeNullOrEmpty();

        // ── Step 5: Verify MFA — should return final tokens ──────────────────
        var verifyResp = await anonClient.PostAsJsonAsync("/api/v1/auth/mfa/verify", new
        {
            tempToken,
            code = FixedCodeTotpService.ValidCode
        });
        verifyResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var verifyBody = await verifyResp.Content.ReadFromJsonAsync<JsonElement>();
        verifyBody.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        verifyBody.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MfaVerify_WithWrongCode_Returns422()
    {
        // Enable MFA on owner account
        var (ownerClient, _, _) = await Auth.LoginAsync(
            TestConstants.OwnerEmail, TestConstants.DemoPassword);

        await ownerClient.PostAsync("/api/v1/auth/mfa/setup", null);
        await ownerClient.PostAsJsonAsync("/api/v1/auth/mfa/confirm", new
        {
            code = FixedCodeTotpService.ValidCode
        });

        // Login to get temp token
        var anonClient = Factory.CreateClient();
        var loginResp = await anonClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email    = TestConstants.OwnerEmail,
            password = TestConstants.DemoPassword
        });
        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        var tempToken = loginBody.GetProperty("tempToken").GetString();

        // Try to verify with a wrong code
        var verifyResp = await anonClient.PostAsJsonAsync("/api/v1/auth/mfa/verify", new
        {
            tempToken,
            code = "000000"
        });

        verifyResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "wrong MFA code should be rejected");
    }
}
