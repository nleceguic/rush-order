namespace RushOrder.API.IntegrationTests.Infrastructure;

public static class TestConstants
{
    // Raw webhook signing secret used in tests (no whsec_ prefix to avoid base64-decoding ambiguity)
    public const string StripeWebhookSecret = "test_webhook_signing_secret_for_integration";

    // Demo tenant seeded by DatabaseSeeder.SeedDevelopmentDataAsync
    public const string OwnerEmail   = "owner@demo.com";
    public const string ManagerEmail = "manager@demo.com";
    public const string WaiterEmail  = "waiter@demo.com";
    public const string KitchenEmail = "kitchen@demo.com";
    public const string DemoPassword = "Demo1234!";

    // Admin seeded in dev environment for tests
    public const string AdminEmail    = "admin@rushorder.app";
    public const string AdminPassword = "Admin1234!";
}
