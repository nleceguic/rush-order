namespace RushOrder.API.IntegrationTests.Infrastructure;

[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly ApiFactory Factory;
    protected readonly AuthHelper Auth;

    protected IntegrationTestBase(ApiFactory factory)
    {
        Factory = factory;
        Auth = new AuthHelper(factory);
    }

    public async Task InitializeAsync() => await Factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
