# QR Session (Backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mint a short-lived, table-scoped JWT when a QR code is resolved, and require it (or an existing staff token) on every backend endpoint that today trusts a client-supplied `TableId`/`OrderId` with no real ownership check.

**Architecture:** Add a third JWT type (`GenerateQrSessionToken`) to the existing `JwtTokenService`/RSA infrastructure, carrying `tid`/`table_id`/`restaurant_id`/`token_use=qr_session` claims and no personal data. `QrController` mints and returns it alongside the existing `TablePublicDto` payload. `ICurrentTenantService` gains `IsQrSession`/`QrSessionTableId`, read from those claims exactly like it already reads `tid`. Four Application-layer handlers (`CreateOrder`, `AddItemToOrder`, `GetOrderById`, `RateOrder`) add one identical guard: if the caller is a QR session, its `table_id` must match the target order's/request's table; if not (staff), behavior is unchanged. No controller-level `[Authorize]` attribute changes are needed except removing `[AllowAnonymous]` from `RateOrder` — `AddItem`/`GetById` already require *some* authenticated principal via `ApiController`'s class-level `[Authorize]`.

**Tech Stack:** .NET 8, ASP.NET Core, MediatR, `System.IdentityModel.Tokens.Jwt`, xUnit + Moq + FluentAssertions (Application.Tests), xUnit + Testcontainers (API.IntegrationTests).

## Global Constraints

- Reuse the existing RSA key pair / `IRsaKeyProvider` / `JwtBearer` scheme as-is — do not introduce a second signing mechanism, issuer, or audience.
- `qr_session` tokens carry only `tid`, `table_id`, `restaurant_id`, `token_use`, `jti`, `iat`, `exp` — never `sub`, `email`, or any `ClaimTypes.Role` claim.
- QR session TTL is 3 hours, configurable via `Jwt:QrSessionExpirationHours` (default `3`), following the existing `AccessTokenExpirationMinutes` pattern in `JwtSettings`.
- Do not touch Postgres RLS policies or `TenantDbCommandInterceptor` — this plan resolves entirely through the existing `tid`-claim → `CurrentTenantService.TenantId` → RLS session-variable path.
- Do not add `[AllowAnonymous]` to `AddItem` or `GetById` — they already require an authenticated principal via `ApiController`'s class-level `[Authorize]`, and that must not be loosened.
- `CancelOrderCommand`/`POST /orders/{id}/cancel` is untouched — out of scope (staff-only today, confirmed with product owner).
- `IOrderVerificationService`/`trackingToken` is untouched — stays unused, not wired to anything by this plan.
- Design source of truth: `docs/product/qr-session-design.md` (approved).
- Frontend wiring (PWA attaching the token, 401 retry, etc.) is explicitly out of scope for this plan — separate follow-up plan.

---

## File Structure

| File | Change |
|---|---|
| `backend/src/RushOrder.Infrastructure/Settings/JwtSettings.cs` | Add `QrSessionExpirationHours` |
| `backend/src/RushOrder.API/appsettings.json`, `appsettings.Development.json` | Add `Jwt:QrSessionExpirationHours` |
| `backend/src/RushOrder.Infrastructure/DependencyInjection.cs` | Map new setting into `services.Configure<JwtSettings>` |
| `backend/src/RushOrder.Application/Common/Interfaces/IJwtTokenService.cs` | Add `GenerateQrSessionToken` signature |
| `backend/src/RushOrder.Infrastructure/Identity/JwtTokenService.cs` | Implement `GenerateQrSessionToken` |
| `backend/src/RushOrder.Application/Common/Interfaces/ICurrentTenantService.cs` | Add `IsQrSession`, `QrSessionTableId` |
| `backend/src/RushOrder.Infrastructure/Services/CurrentTenantService.cs` | Populate the two new properties from claims |
| `backend/src/RushOrder.Application/Tables/DTOs/TablePublicDto.cs` | Add `SessionToken`, `SessionExpiresAt` |
| `backend/src/RushOrder.Application/Tables/Queries/GetTableByQrCodeQuery.cs` | Mint the session token in the handler |
| `backend/src/RushOrder.Application/Orders/Commands/CreateOrderCommand.cs` | Add QR-session/table-match guard |
| `backend/src/RushOrder.Application/Orders/Commands/AddItemToOrderCommand.cs` | Add QR-session/table-match guard |
| `backend/src/RushOrder.Application/Orders/Queries/GetOrderByIdQuery.cs` | Add QR-session/table-match guard |
| `backend/src/RushOrder.Application/Orders/Commands/RateOrderCommand.cs` | Add QR-session/table-match guard |
| `backend/src/RushOrder.API/Controllers/OrdersController.cs` | Remove `[AllowAnonymous]` from `RateOrder` |
| Various `*Tests.cs` under `backend/tests/` | New/updated tests per task below |

---

### Task 1: QR session token minting (`JwtTokenService`)

**Files:**
- Modify: `backend/src/RushOrder.Infrastructure/Settings/JwtSettings.cs`
- Modify: `backend/src/RushOrder.API/appsettings.json`
- Modify: `backend/src/RushOrder.API/appsettings.Development.json`
- Modify: `backend/src/RushOrder.Infrastructure/DependencyInjection.cs:141-149`
- Modify: `backend/src/RushOrder.Application/Common/Interfaces/IJwtTokenService.cs`
- Modify: `backend/src/RushOrder.Infrastructure/Identity/JwtTokenService.cs`
- Test: Create `backend/tests/RushOrder.API.IntegrationTests/Identity/JwtTokenServiceQrSessionTests.cs`

**Interfaces:**
- Produces: `IJwtTokenService.GenerateQrSessionToken(Guid tableId, Guid restaurantId, Guid tenantId) : AccessTokenResult` — `AccessTokenResult(string Token, string Jti, DateTimeOffset ExpiresAt)` (existing record, unchanged).

- [ ] **Step 1: Write the failing test**

Create `backend/tests/RushOrder.API.IntegrationTests/Identity/JwtTokenServiceQrSessionTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RushOrder.API.IntegrationTests.Infrastructure;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.API.IntegrationTests.Identity;

// Resolves the real JwtTokenService/FileRsaKeyProvider from the DI container —
// no HTTP call needed, this exercises the token shape directly.
public sealed class JwtTokenServiceQrSessionTests : IntegrationTestBase
{
    public JwtTokenServiceQrSessionTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public void GenerateQrSessionToken_HasExpectedClaimsAndNoPersonalData()
    {
        using var scope = Factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var tableId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var result = tokenService.GenerateQrSessionToken(tableId, restaurantId, tenantId);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Claims.First(c => c.Type == "tid").Value.Should().Be(tenantId.ToString());
        jwt.Claims.First(c => c.Type == "table_id").Value.Should().Be(tableId.ToString());
        jwt.Claims.First(c => c.Type == "restaurant_id").Value.Should().Be(restaurantId.ToString());
        jwt.Claims.First(c => c.Type == "token_use").Value.Should().Be("qr_session");

        jwt.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Sub).Should().BeFalse();
        jwt.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Email).Should().BeFalse();
        jwt.Claims.Any(c => c.Type == ClaimTypes.Role).Should().BeFalse();

        result.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(3), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GenerateQrSessionToken_ProducesTokenThatValidatesAgainstTheSameService()
    {
        using var scope = Factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var result = tokenService.GenerateQrSessionToken(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var principal = tokenService.ValidateToken(result.Token);

        principal.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/RushOrder.API.IntegrationTests --filter JwtTokenServiceQrSessionTests`
Expected: FAIL — `IJwtTokenService` has no `GenerateQrSessionToken` method (compile error).

- [ ] **Step 3: Add the setting**

In `backend/src/RushOrder.Infrastructure/Settings/JwtSettings.cs`, add a property:

```csharp
public sealed class JwtSettings
{
    public string Issuer { get; set; } = "RushOrder";
    public string Audience { get; set; } = "RushOrder.API";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 30;
    public int QrSessionExpirationHours { get; set; } = 3;
    public string PrivateKeyPath { get; set; } = "keys/private.pem";
    public string PublicKeyPath { get; set; } = "keys/public.pem";
}
```

In `backend/src/RushOrder.API/appsettings.json` and `appsettings.Development.json`, add `"QrSessionExpirationHours": 3` to the `Jwt` section (after `"RefreshTokenExpirationDays": 30,`):

```json
  "Jwt": {
    "Issuer": "rush-order-api",
    "Audience": "rush-order-clients",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 30,
    "QrSessionExpirationHours": 3,
    "PrivateKeyPath": "keys/private.pem",
    "PublicKeyPath": "keys/public.pem"
  },
```

In `backend/src/RushOrder.Infrastructure/DependencyInjection.cs`, extend the existing `services.Configure<JwtSettings>` block (around line 141-149):

```csharp
        services.Configure<JwtSettings>(opts =>
        {
            opts.Issuer = jwtSettings.Issuer;
            opts.Audience = jwtSettings.Audience;
            opts.AccessTokenExpirationMinutes = jwtSettings.AccessTokenExpirationMinutes;
            opts.RefreshTokenExpirationDays = jwtSettings.RefreshTokenExpirationDays;
            opts.QrSessionExpirationHours = jwtSettings.QrSessionExpirationHours;
            opts.PrivateKeyPath = jwtSettings.PrivateKeyPath;
            opts.PublicKeyPath = jwtSettings.PublicKeyPath;
        });
```

- [ ] **Step 4: Add the interface method**

In `backend/src/RushOrder.Application/Common/Interfaces/IJwtTokenService.cs`:

```csharp
public interface IJwtTokenService
{
    AccessTokenResult GenerateAccessToken(User user, Guid tenantId);
    AccessTokenResult GenerateImpersonationToken(Guid adminUserId, string adminEmail, Guid targetTenantId);
    AccessTokenResult GenerateQrSessionToken(Guid tableId, Guid restaurantId, Guid tenantId);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}
```

- [ ] **Step 5: Implement it in `JwtTokenService`**

In `backend/src/RushOrder.Infrastructure/Identity/JwtTokenService.cs`, add a new method (after `GenerateImpersonationToken`, before `GenerateRefreshToken`):

```csharp
    public AccessTokenResult GenerateQrSessionToken(Guid tableId, Guid restaurantId, Guid tenantId)
    {
        var jti = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(_settings.QrSessionExpirationHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("tid", tenantId.ToString()),
            new("table_id", tableId.ToString()),
            new("restaurant_id", restaurantId.ToString()),
            new("token_use", "qr_session")
        };

        var signingKey = new RsaSecurityKey(_rsaKeyProvider.GetPrivateKey());
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        return new AccessTokenResult(token, jti, expires);
    }
```

No new `using` directives needed — this method uses only types already imported at the top of the file (`System.IdentityModel.Tokens.Jwt`, `System.Security.Claims`, `Microsoft.IdentityModel.Tokens`).

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test backend/tests/RushOrder.API.IntegrationTests --filter JwtTokenServiceQrSessionTests`
Expected: PASS (2 tests)

- [ ] **Step 7: Commit**

```bash
git add backend/src/RushOrder.Infrastructure/Settings/JwtSettings.cs backend/src/RushOrder.API/appsettings.json backend/src/RushOrder.API/appsettings.Development.json backend/src/RushOrder.Infrastructure/DependencyInjection.cs backend/src/RushOrder.Application/Common/Interfaces/IJwtTokenService.cs backend/src/RushOrder.Infrastructure/Identity/JwtTokenService.cs backend/tests/RushOrder.API.IntegrationTests/Identity/JwtTokenServiceQrSessionTests.cs
git commit -m "feat(auth): add QR session token generation to JwtTokenService"
```

---

### Task 2: `ICurrentTenantService` reads the QR session claims

**Files:**
- Modify: `backend/src/RushOrder.Application/Common/Interfaces/ICurrentTenantService.cs`
- Modify: `backend/src/RushOrder.Infrastructure/Services/CurrentTenantService.cs`
- Test: Create `backend/tests/RushOrder.API.IntegrationTests/Identity/CurrentTenantServiceTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `ICurrentTenantService.IsQrSession : bool`, `ICurrentTenantService.QrSessionTableId : Guid?` — consumed by Tasks 4-7.

- [ ] **Step 1: Write the failing test**

Create `backend/tests/RushOrder.API.IntegrationTests/Identity/CurrentTenantServiceTests.cs`:

```csharp
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using RushOrder.Infrastructure.Services;

namespace RushOrder.API.IntegrationTests.Identity;

// Plain unit test — CurrentTenantService only depends on IHttpContextAccessor,
// which is faked directly here. No database, no ApiFactory needed.
public sealed class CurrentTenantServiceTests
{
    private static CurrentTenantService BuildService(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new CurrentTenantService(accessor);
    }

    [Fact]
    public void Constructor_QrSessionClaims_SetsIsQrSessionAndTableId()
    {
        var tenantId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
        [
            new Claim("tid", tenantId.ToString()),
            new Claim("table_id", tableId.ToString()),
            new Claim("token_use", "qr_session"),
        ], authenticationType: "Bearer");

        var service = BuildService(new ClaimsPrincipal(identity));

        service.IsQrSession.Should().BeTrue();
        service.QrSessionTableId.Should().Be(tableId);
        service.TenantId.Should().Be(tenantId);
        service.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void Constructor_StaffClaims_IsQrSessionFalseAndTableIdNull()
    {
        var tenantId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
        [
            new Claim("tid", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Owner"),
        ], authenticationType: "Bearer");

        var service = BuildService(new ClaimsPrincipal(identity));

        service.IsQrSession.Should().BeFalse();
        service.QrSessionTableId.Should().BeNull();
        service.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void Constructor_NoClaims_AllFalseOrNull()
    {
        var service = BuildService(new ClaimsPrincipal());

        service.IsAuthenticated.Should().BeFalse();
        service.IsQrSession.Should().BeFalse();
        service.QrSessionTableId.Should().BeNull();
        service.TenantId.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/RushOrder.API.IntegrationTests --filter CurrentTenantServiceTests`
Expected: FAIL — `CurrentTenantService` has no `IsQrSession`/`QrSessionTableId` members (compile error).

- [ ] **Step 3: Extend the interface**

In `backend/src/RushOrder.Application/Common/Interfaces/ICurrentTenantService.cs`:

```csharp
namespace RushOrder.Application.Common.Interfaces;

public interface ICurrentTenantService
{
    Guid? TenantId { get; }
    bool IsAuthenticated { get; }
    bool IsQrSession { get; }
    Guid? QrSessionTableId { get; }
}
```

- [ ] **Step 4: Implement it in `CurrentTenantService`**

Replace the full contents of `backend/src/RushOrder.Infrastructure/Services/CurrentTenantService.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Services;

public sealed class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; }
    public bool IsAuthenticated { get; }
    public bool IsQrSession { get; }
    public Guid? QrSessionTableId { get; }

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        IsAuthenticated = user?.Identity?.IsAuthenticated ?? false;

        var tidClaim = user?.FindFirstValue("tid");
        if (tidClaim is not null && Guid.TryParse(tidClaim, out var tenantId))
            TenantId = tenantId;

        IsQrSession = user?.FindFirstValue("token_use") == "qr_session";

        var tableIdClaim = user?.FindFirstValue("table_id");
        if (tableIdClaim is not null && Guid.TryParse(tableIdClaim, out var tableId))
            QrSessionTableId = tableId;
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test backend/tests/RushOrder.API.IntegrationTests --filter CurrentTenantServiceTests`
Expected: PASS (3 tests)

- [ ] **Step 6: Run the full existing suite to check for regressions**

Run: `dotnet test backend/tests/RushOrder.API.IntegrationTests`
Expected: PASS — `ICurrentTenantService` is an interface consumed by mocks elsewhere (`Mock<ICurrentTenantService>` in Application.Tests), so adding members to it doesn't break any existing implementer other than `CurrentTenantService` itself, which was just updated.

- [ ] **Step 7: Commit**

```bash
git add backend/src/RushOrder.Application/Common/Interfaces/ICurrentTenantService.cs backend/src/RushOrder.Infrastructure/Services/CurrentTenantService.cs backend/tests/RushOrder.API.IntegrationTests/Identity/CurrentTenantServiceTests.cs
git commit -m "feat(auth): expose QR session claims via ICurrentTenantService"
```

---

### Task 3: `QrController` mints and returns the session token

**Files:**
- Modify: `backend/src/RushOrder.Application/Tables/DTOs/TablePublicDto.cs`
- Modify: `backend/src/RushOrder.Application/Tables/Queries/GetTableByQrCodeQuery.cs`
- Modify: `backend/tests/RushOrder.Application.Tests/Tables/Queries/GetTableByQrCodeQueryHandlerTests.cs`
- Modify: `backend/tests/RushOrder.API.IntegrationTests/Qr/QrTests.cs`

**Interfaces:**
- Consumes: `IJwtTokenService.GenerateQrSessionToken` (Task 1).
- Produces: `TablePublicDto.SessionToken : string`, `TablePublicDto.SessionExpiresAt : DateTimeOffset` — this is the field the (future, separate) frontend plan will read.

- [ ] **Step 1: Write the failing unit test**

In `backend/tests/RushOrder.Application.Tests/Tables/Queries/GetTableByQrCodeQueryHandlerTests.cs`, add the mock and update every constructor call and the happy-path assertion. Replace the top of the class and the `Handle_ValidQrCode_ReturnsTableAndRestaurantData` test:

```csharp
using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Tables.Queries;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Tests.Tables.Queries;

public sealed class GetTableByQrCodeQueryHandlerTests
{
    private readonly Mock<ITableRepository> _tableRepo = new();
    private readonly Mock<IRestaurantRepository> _restaurantRepo = new();
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();

    private readonly GetTableByQrCodeQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public GetTableByQrCodeQueryHandlerTests()
    {
        _jwtTokenService
            .Setup(s => s.GenerateQrSessionToken(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(new AccessTokenResult("test-session-token", "test-jti", DateTimeOffset.UtcNow.AddHours(3)));

        _handler = new GetTableByQrCodeQueryHandler(_tableRepo.Object, _restaurantRepo.Object, _jwtTokenService.Object);
    }
```

Then, in `Handle_ValidQrCode_ReturnsTableAndRestaurantData`, append two assertions after `result.VatRate.Should().Be(0.10m);`:

```csharp
        result.SessionToken.Should().Be("test-session-token");
        result.SessionExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(3), TimeSpan.FromMinutes(1));
```

Also add a test verifying the handler passes the right IDs through:

```csharp
    [Fact]
    public async Task Handle_ValidQrCode_MintsSessionTokenForThatTableAndTenant()
    {
        var table = Table.Create(TenantId, RestaurantId, "Mesa 5", 4, "Terraza");
        var restaurant = Restaurant.Create(TenantId, "El Restaurante", "Calle 1", "+34600000000", "test@r.com");

        _tableRepo.Setup(r => r.GetByQrCodeAsync(table.QrCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(table);
        _restaurantRepo.Setup(r => r.GetByIdPublicAsync(RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(restaurant);

        await _handler.Handle(new GetTableByQrCodeQuery(table.QrCode), CancellationToken.None);

        _jwtTokenService.Verify(
            s => s.GenerateQrSessionToken(table.Id, table.RestaurantId, table.TenantId),
            Times.Once);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter GetTableByQrCodeQueryHandlerTests`
Expected: FAIL — constructor arity mismatch / `TablePublicDto` has no `SessionToken` member (compile error).

- [ ] **Step 3: Add the DTO fields**

In `backend/src/RushOrder.Application/Tables/DTOs/TablePublicDto.cs`:

```csharp
namespace RushOrder.Application.Tables.DTOs;

public record TablePublicDto(
    Guid TableId,
    string Name,
    int Capacity,
    string? Zone,
    Guid RestaurantId,
    string RestaurantName,
    string Currency,
    string? LogoUrl,
    string? CoverImageUrl,
    bool UpsellingEnabled,
    IReadOnlyList<string> AvailableLocales,
    decimal VatRate,
    bool OnlinePaymentEnabled,
    string? WelcomeMessage,
    string SessionToken,
    DateTimeOffset SessionExpiresAt);
```

- [ ] **Step 4: Mint the token in the handler**

Replace `backend/src/RushOrder.Application/Tables/Queries/GetTableByQrCodeQuery.cs`:

```csharp
using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Tables.DTOs;

namespace RushOrder.Application.Tables.Queries;

public record GetTableByQrCodeQuery(string QrCode) : IQuery<TablePublicDto?>;

public sealed class GetTableByQrCodeQueryHandler : IRequestHandler<GetTableByQrCodeQuery, TablePublicDto?>
{
    private readonly ITableRepository _tableRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IJwtTokenService _jwtTokenService;

    public GetTableByQrCodeQueryHandler(
        ITableRepository tableRepository,
        IRestaurantRepository restaurantRepository,
        IJwtTokenService jwtTokenService)
    {
        _tableRepository = tableRepository;
        _restaurantRepository = restaurantRepository;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<TablePublicDto?> Handle(GetTableByQrCodeQuery request, CancellationToken cancellationToken)
    {
        var table = await _tableRepository.GetByQrCodeAsync(request.QrCode, cancellationToken);
        if (table is null) return null;

        var restaurant = await _restaurantRepository.GetByIdPublicAsync(table.RestaurantId, cancellationToken);
        if (restaurant is null || !restaurant.IsActive) return null;

        var session = _jwtTokenService.GenerateQrSessionToken(table.Id, table.RestaurantId, table.TenantId);

        return new TablePublicDto(
            table.Id,
            table.Name,
            table.Capacity,
            table.Zone,
            restaurant.Id,
            restaurant.Name,
            restaurant.Currency,
            restaurant.LogoUrl,
            restaurant.CoverUrl,
            restaurant.Settings.UpsellingEnabled,
            AvailableLocales: ["es"],
            restaurant.TaxRate,
            OnlinePaymentEnabled: restaurant.StripeAccountId is not null,
            WelcomeMessage: null,
            SessionToken: session.Token,
            SessionExpiresAt: session.ExpiresAt);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter GetTableByQrCodeQueryHandlerTests`
Expected: PASS (7 tests)

- [ ] **Step 6: Write the failing integration test**

In `backend/tests/RushOrder.API.IntegrationTests/Qr/QrTests.cs`, add `using System.IdentityModel.Tokens.Jwt;` to the top, and add a new test after `GetByQrCode_WithValidQrCode_Returns200WithTableAndRestaurantData`:

```csharp
    [Fact]
    public async Task GetByQrCode_WithValidQrCode_ReturnsSessionTokenBoundToThatTable()
    {
        var qrCode = await GetSeededQrCodeAsync();
        var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/qr/{qrCode}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        var tableId = data.GetProperty("tableId").GetGuid();
        var sessionToken = data.GetProperty("sessionToken").GetString();

        sessionToken.Should().NotBeNullOrEmpty();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(sessionToken);
        jwt.Claims.First(c => c.Type == "table_id").Value.Should().Be(tableId.ToString());
        jwt.Claims.First(c => c.Type == "token_use").Value.Should().Be("qr_session");
    }
```

- [ ] **Step 7: Run test to verify it fails, then passes**

Run: `dotnet test backend/tests/RushOrder.API.IntegrationTests --filter QrTests`
Before Step 4's handler change this fails with a `KeyNotFoundException`-style JSON error (no `sessionToken` property); after it, expected: PASS (4 tests).

- [ ] **Step 8: Commit**

```bash
git add backend/src/RushOrder.Application/Tables/DTOs/TablePublicDto.cs backend/src/RushOrder.Application/Tables/Queries/GetTableByQrCodeQuery.cs backend/tests/RushOrder.Application.Tests/Tables/Queries/GetTableByQrCodeQueryHandlerTests.cs backend/tests/RushOrder.API.IntegrationTests/Qr/QrTests.cs
git commit -m "feat(qr): mint and return QR session token from GET /qr/{qrCode}"
```

---

### Task 4: `CreateOrder` — enforce QR session table match

**Files:**
- Modify: `backend/src/RushOrder.Application/Orders/Commands/CreateOrderCommand.cs`
- Modify: `backend/tests/RushOrder.Application.Tests/Orders/Commands/CreateOrderCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ICurrentTenantService.IsQrSession`, `ICurrentTenantService.QrSessionTableId` (Task 2).

- [ ] **Step 1: Write the failing tests**

In `backend/tests/RushOrder.Application.Tests/Orders/Commands/CreateOrderCommandHandlerTests.cs`, add after `Handle_NoTenantContext_ThrowsUnauthorizedAccessException`:

```csharp
    [Fact]
    public async Task Handle_QrSessionTableMismatch_ThrowsUnauthorizedAccessException()
    {
        _tenantService.Setup(s => s.TenantId).Returns(TenantId);
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(Guid.NewGuid()); // different table

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_QrSessionTableMatch_Succeeds()
    {
        SetupHappyPath();
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(TableId);

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        result.OrderId.Should().NotBeEmpty();
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter CreateOrderCommandHandlerTests`
Expected: `Handle_QrSessionTableMismatch_ThrowsUnauthorizedAccessException` FAILS (no exception thrown today); `Handle_QrSessionTableMatch_Succeeds` passes already (no guard yet, so it's not a useful negative signal on its own — the mismatch test is the one that proves the guard is missing).

- [ ] **Step 3: Add the guard**

In `backend/src/RushOrder.Application/Orders/Commands/CreateOrderCommand.cs`, in `CreateOrderCommandHandler.Handle`, right after the existing tenant check:

```csharp
        var tenantId = _tenantService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        if (_tenantService.IsQrSession && _tenantService.QrSessionTableId != request.TableId)
            throw new UnauthorizedAccessException("QR session does not match the requested table.");

        var table = await _tableRepository.GetByIdAsync(request.TableId, cancellationToken)
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter CreateOrderCommandHandlerTests`
Expected: PASS (all tests, including the two new ones and the pre-existing ones — `IsQrSession` defaults to `false` on a bare `Mock<ICurrentTenantService>`, so every other existing test is unaffected).

- [ ] **Step 5: Commit**

```bash
git add backend/src/RushOrder.Application/Orders/Commands/CreateOrderCommand.cs backend/tests/RushOrder.Application.Tests/Orders/Commands/CreateOrderCommandHandlerTests.cs
git commit -m "feat(orders): reject CreateOrder when QR session table does not match"
```

---

### Task 5: `AddItemToOrder` — enforce QR session table match

**Files:**
- Modify: `backend/src/RushOrder.Application/Orders/Commands/AddItemToOrderCommand.cs`
- Create: `backend/tests/RushOrder.Application.Tests/Orders/Commands/AddItemToOrderCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ICurrentTenantService.IsQrSession`, `ICurrentTenantService.QrSessionTableId` (Task 2).

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/RushOrder.Application.Tests/Orders/Commands/AddItemToOrderCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.Commands;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Tests.Orders.Commands;

public sealed class AddItemToOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IProductRepository> _productRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentTenantService> _tenantService = new();

    private readonly AddItemToOrderCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid TableId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public AddItemToOrderCommandHandlerTests()
    {
        _handler = new AddItemToOrderCommandHandler(
            _orderRepo.Object, _productRepo.Object, _unitOfWork.Object, _tenantService.Object);
    }

    private Order BuildOrder()
        => Order.Create(TenantId, RestaurantId, TableId, 1, OrderSource.QR);

    private Product BuildProduct(bool isAvailable = true)
    {
        var product = Product.Create(TenantId, RestaurantId, CategoryId, "Agua Mineral", new Money(1.50m, "EUR"));
        if (!isAvailable) product.SetAvailability(false);
        return product;
    }

    private AddItemToOrderCommand BuildCommand() => new(OrderId, ProductId, Quantity: 2);

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsNotFoundException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Order*");
    }

    [Fact]
    public async Task Handle_StaffCaller_AddsItemRegardlessOfTable()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _productRepo.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildProduct());
        // IsQrSession defaults to false on a bare mock — this is the staff path.

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_QrSessionMatchingTable_AddsItem()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _productRepo.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildProduct());
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(TableId);

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        _orderRepo.Verify(r => r.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_QrSessionWrongTable_ThrowsUnauthorizedAccessException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(Guid.NewGuid()); // different table

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_UnavailableProduct_ThrowsBusinessRuleException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _productRepo.Setup(r => r.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProduct(isAvailable: false));

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*not available*");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter AddItemToOrderCommandHandlerTests`
Expected: FAIL — constructor arity mismatch (`AddItemToOrderCommandHandler` doesn't take `ICurrentTenantService` yet) — compile error.

- [ ] **Step 3: Add the guard**

Replace `backend/src/RushOrder.Application/Orders/Commands/AddItemToOrderCommand.cs`:

```csharp
using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Orders.Commands;

// --- Command ---

public record AddItemToOrderCommand(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    string? Notes = null) : ICommand<Unit>;

// --- Validator ---

public sealed class AddItemToOrderCommandValidator : AbstractValidator<AddItemToOrderCommand>
{
    public AddItemToOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, 50);
    }
}

// --- Handler ---

public sealed class AddItemToOrderCommandHandler : IRequestHandler<AddItemToOrderCommand, Unit>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;

    public AddItemToOrderCommandHandler(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<Unit> Handle(AddItemToOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (_tenantService.IsQrSession && _tenantService.QrSessionTableId != order.TableId)
            throw new UnauthorizedAccessException("QR session does not match the order's table.");

        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
            throw new BusinessRuleException(
                $"Items can only be added to Pending or Confirmed orders. Current status: {order.Status}.");

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (!product.IsAvailable)
            throw new BusinessRuleException($"Product '{product.Name}' is not available.");

        order.AddItem(product.Id, product.Name, product.Price, request.Quantity, request.Notes);

        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter AddItemToOrderCommandHandlerTests`
Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add backend/src/RushOrder.Application/Orders/Commands/AddItemToOrderCommand.cs backend/tests/RushOrder.Application.Tests/Orders/Commands/AddItemToOrderCommandHandlerTests.cs
git commit -m "feat(orders): reject AddItemToOrder when QR session table does not match"
```

---

### Task 6: `GetOrderById` — enforce QR session table match

**Files:**
- Modify: `backend/src/RushOrder.Application/Orders/Queries/GetOrderByIdQuery.cs`
- Create: `backend/tests/RushOrder.Application.Tests/Orders/Queries/GetOrderByIdQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `ICurrentTenantService.IsQrSession`, `ICurrentTenantService.QrSessionTableId` (Task 2).

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/RushOrder.Application.Tests/Orders/Queries/GetOrderByIdQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.Queries;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Tests.Orders.Queries;

public sealed class GetOrderByIdQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<ITableRepository> _tableRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentTenantService> _tenantService = new();

    private readonly GetOrderByIdQueryHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid TableId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public GetOrderByIdQueryHandlerTests()
    {
        _handler = new GetOrderByIdQueryHandler(_orderRepo.Object, _tableRepo.Object, _userRepo.Object, _tenantService.Object);
    }

    private Order BuildOrder() => Order.Create(TenantId, RestaurantId, TableId, 1, OrderSource.QR);

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNull()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        var result = await _handler.Handle(new GetOrderByIdQuery(OrderId), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_StaffCaller_ReturnsOrderRegardlessOfTable()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        // IsQrSession defaults to false on a bare mock — this is the staff path.

        var result = await _handler.Handle(new GetOrderByIdQuery(OrderId), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_QrSessionMatchingTable_ReturnsOrder()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(TableId);

        var result = await _handler.Handle(new GetOrderByIdQuery(OrderId), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_QrSessionWrongTable_ThrowsUnauthorizedAccessException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(Guid.NewGuid()); // different table

        var act = () => _handler.Handle(new GetOrderByIdQuery(OrderId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter GetOrderByIdQueryHandlerTests`
Expected: FAIL — constructor arity mismatch (compile error).

- [ ] **Step 3: Add the guard**

Replace `backend/src/RushOrder.Application/Orders/Queries/GetOrderByIdQuery.cs`:

```csharp
using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Orders.Queries;

// --- Query ---

public record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDetailDto?>;

// --- Handler ---

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailDto?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITableRepository _tableRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentTenantService _tenantService;

    public GetOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        ITableRepository tableRepository,
        IUserRepository userRepository,
        ICurrentTenantService tenantService)
    {
        _orderRepository = orderRepository;
        _tableRepository = tableRepository;
        _userRepository = userRepository;
        _tenantService = tenantService;
    }

    public async Task<OrderDetailDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null) return null;

        if (_tenantService.IsQrSession && _tenantService.QrSessionTableId != order.TableId)
            throw new UnauthorizedAccessException("QR session does not match the order's table.");

        var table = await _tableRepository.GetByIdAsync(order.TableId, cancellationToken);

        User? waiter = null;
        if (order.WaiterId.HasValue)
            waiter = await _userRepository.GetByIdAsync(order.WaiterId.Value, cancellationToken);

        return ToDetailDto(order, table, waiter);
    }

    internal static OrderDetailDto ToDetailDto(Order order, Table? table, User? waiter)
        => new(
            order.Id,
            order.OrderNumber,
            order.RestaurantId,
            order.TableId,
            table?.Name,
            order.WaiterId,
            waiter?.FullName,
            order.CustomerId,
            order.Status.ToString(),
            order.Source.ToString(),
            order.Subtotal.Amount,
            order.TaxAmount.Amount,
            order.DiscountAmount.Amount,
            order.TipAmount.Amount,
            order.Total.Amount,
            order.Total.Currency,
            order.Notes,
            order.EstimatedReadyAt,
            order.CancellationReason,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items.Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.Name,
                i.UnitPrice.Amount,
                i.UnitPrice.Currency,
                i.Quantity,
                i.Notes,
                i.Modifiers,
                i.LineTotal.Amount)).ToList().AsReadOnly());
}
```

(Only the constructor, the new guard line, and the added `ICurrentTenantService` field/using change — `ToDetailDto` is unchanged.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter GetOrderByIdQueryHandlerTests`
Expected: PASS (4 tests)

- [ ] **Step 5: Run the full Application.Tests suite to check for regressions**

Run: `dotnet test backend/tests/RushOrder.Application.Tests`
Expected: PASS — no other handler constructs `GetOrderByIdQueryHandler` directly.

- [ ] **Step 6: Commit**

```bash
git add backend/src/RushOrder.Application/Orders/Queries/GetOrderByIdQuery.cs backend/tests/RushOrder.Application.Tests/Orders/Queries/GetOrderByIdQueryHandlerTests.cs
git commit -m "feat(orders): reject GetOrderById when QR session table does not match"
```

---

### Task 7: `RateOrder` — require authentication, enforce QR session table match

**Files:**
- Modify: `backend/src/RushOrder.API/Controllers/OrdersController.cs`
- Modify: `backend/src/RushOrder.Application/Orders/Commands/RateOrderCommand.cs`
- Create: `backend/tests/RushOrder.Application.Tests/Orders/Commands/RateOrderCommandHandlerTests.cs`
- Modify: `backend/tests/RushOrder.API.IntegrationTests/Orders/OrderTests.cs`

**Interfaces:**
- Consumes: `ICurrentTenantService.IsQrSession`, `ICurrentTenantService.QrSessionTableId` (Task 2).

- [ ] **Step 1: Write the failing unit tests**

Create `backend/tests/RushOrder.Application.Tests/Orders/Commands/RateOrderCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using Moq;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Orders.Commands;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Tests.Orders.Commands;

public sealed class RateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<IOrderRatingRepository> _ratingRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentTenantService> _tenantService = new();

    private readonly RateOrderCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid TableId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public RateOrderCommandHandlerTests()
    {
        _handler = new RateOrderCommandHandler(_orderRepo.Object, _ratingRepo.Object, _unitOfWork.Object, _tenantService.Object);
    }

    private Order BuildOrder() => Order.Create(TenantId, RestaurantId, TableId, 1, OrderSource.QR);

    private RateOrderCommand BuildCommand() => new(OrderId, Food: 5, Speed: 4, Service: 5, Comment: "Genial");

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsNotFoundException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync((Order?)null);

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*Order*");
    }

    [Fact]
    public async Task Handle_QrSessionMatchingTable_CreatesRating()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _ratingRepo.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync((OrderRating?)null);
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(TableId);

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        _ratingRepo.Verify(r => r.AddAsync(It.IsAny<OrderRating>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_QrSessionWrongTable_ThrowsUnauthorizedAccessException()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(Guid.NewGuid()); // different table

        var act = () => _handler.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_AlreadyRated_IsIdempotent()
    {
        _orderRepo.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildOrder());
        _ratingRepo.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderRating.Create(TenantId, OrderId, RestaurantId, 5, 5, 5, null));
        _tenantService.Setup(s => s.IsQrSession).Returns(true);
        _tenantService.Setup(s => s.QrSessionTableId).Returns(TableId);

        await _handler.Handle(BuildCommand(), CancellationToken.None);

        _ratingRepo.Verify(r => r.AddAsync(It.IsAny<OrderRating>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter RateOrderCommandHandlerTests`
Expected: FAIL — constructor arity mismatch (compile error).

- [ ] **Step 3: Add the guard and remove `[AllowAnonymous]`**

Replace `backend/src/RushOrder.Application/Orders/Commands/RateOrderCommand.cs`:

```csharp
using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Orders.Commands;

// Matches the PWA's RatingSheet exactly: food/speed/service, 1-5 each,
// optional comment. Requires a QR session token (or staff auth) — the
// caller's table must match the order's table; see
// docs/product/qr-session-design.md.
public record RateOrderCommand(
    Guid OrderId, int Food, int Speed, int Service, string? Comment) : ICommand<Unit>;

public sealed class RateOrderCommandValidator : AbstractValidator<RateOrderCommand>
{
    public RateOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Food).InclusiveBetween(1, 5);
        RuleFor(x => x.Speed).InclusiveBetween(1, 5);
        RuleFor(x => x.Service).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

public sealed class RateOrderCommandHandler : IRequestHandler<RateOrderCommand, Unit>
{
    private readonly IOrderRepository _orders;
    private readonly IOrderRatingRepository _ratings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;

    public RateOrderCommandHandler(
        IOrderRepository orders,
        IOrderRatingRepository ratings,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService)
    {
        _orders = orders;
        _ratings = ratings;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<Unit> Handle(RateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (_tenantService.IsQrSession && _tenantService.QrSessionTableId != order.TableId)
            throw new UnauthorizedAccessException("QR session does not match the order's table.");

        var existing = await _ratings.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existing is not null) return Unit.Value; // idempotent — one rating per order

        var rating = OrderRating.Create(
            order.TenantId, order.Id, order.RestaurantId,
            request.Food, request.Speed, request.Service, request.Comment);

        await _ratings.AddAsync(rating, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
```

In `backend/src/RushOrder.API/Controllers/OrdersController.cs`, remove the `[AllowAnonymous]` line from the `RateOrder` action (around line 128), so it becomes:

```csharp
    // POST /api/v1/orders/{id}/rating
    [HttpPost("{id:guid}/rating")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> RateOrder(Guid id, [FromBody] RateOrderRequest req, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await Mediator.Send(new RateOrderCommand(id, req.Food, req.Speed, req.Service, req.Comment), ct);
            return NoContent();
        });
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/RushOrder.Application.Tests --filter RateOrderCommandHandlerTests`
Expected: PASS (4 tests)

- [ ] **Step 5: Write the failing integration test**

In `backend/tests/RushOrder.API.IntegrationTests/Orders/OrderTests.cs`, add after `CreateOrder_WithNonExistentTable_Returns404`:

```csharp
    [Fact]
    public async Task RateOrder_WithoutAuth_Returns401()
    {
        var ownerClient = await Auth.GetOwnerClientAsync();
        var (_, tableId, productId) = await GetSeededIdsAsync();

        var createResp = await ownerClient.PostAsJsonAsync("/api/v1/orders", new
        {
            tableId,
            customerId = (Guid?)null,
            items = new[] { new { productId, quantity = 1, notes = (string?)null, modifiers = Array.Empty<string>() } },
            notes = (string?)null,
            source = "QR"
        });
        var orderId = (await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("orderId").GetString();

        var anonClient = Factory.CreateClient();
        var response = await anonClient.PostAsJsonAsync($"/api/v1/orders/{orderId}/rating", new
        {
            food = 5, speed = 5, service = 5, comment = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
```

- [ ] **Step 6: Run test to verify it fails, then passes**

Run: `dotnet test backend/tests/RushOrder.API.IntegrationTests --filter OrderTests`
Before Step 3's controller change: FAILS (endpoint still `[AllowAnonymous]`, would return 204 or 404, not 401). After: PASS (all `OrderTests`).

- [ ] **Step 7: Run the full backend test suite**

Run: `dotnet test backend`
Expected: PASS — all Domain, Application, and API.IntegrationTests projects green.

- [ ] **Step 8: Commit**

```bash
git add backend/src/RushOrder.API/Controllers/OrdersController.cs backend/src/RushOrder.Application/Orders/Commands/RateOrderCommand.cs backend/tests/RushOrder.Application.Tests/Orders/Commands/RateOrderCommandHandlerTests.cs backend/tests/RushOrder.API.IntegrationTests/Orders/OrderTests.cs
git commit -m "feat(orders): require QR session or staff auth for RateOrder"
```

---

## Self-Review Notes

- **Spec coverage**: §1 (JWT shape) → Task 1. §2 (endpoints) → Tasks 4-7, with the corrected `[Authorize]`-inheritance understanding baked into each task's file list (no attribute changes on `CreateOrder`/`AddItem`/`GetById`, only removing `[AllowAnonymous]` from `RateOrder`). §3/§4 (expiry, back-compat) are frontend-facing and explicitly deferred to the follow-up plan — no backend task needed since the JWT's own `exp` claim and the existing `GlobalExceptionHandler` → 401 mapping already provide everything the backend side of that behavior requires. §5 (RegenerateQrCommand independence) requires no code change — it's a documented non-change (the token never references the QR string, only `table_id`), verified by inspection during design, not by a new test.
- **Placeholder scan**: no TBD/TODO markers; every step has literal code, not a description of code.
- **Type consistency**: `IsQrSession`/`QrSessionTableId` names and the `tid`/`table_id`/`restaurant_id`/`token_use` claim strings are identical across Tasks 1-7 (checked against Task 1's `JwtTokenService` and Task 2's `CurrentTenantService`).
