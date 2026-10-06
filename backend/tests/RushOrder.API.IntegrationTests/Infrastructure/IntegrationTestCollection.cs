// Fixtures start the API in-process and Program configures the static Serilog
// logger, so two hosts booting in parallel race on it. Run collections serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RushOrder.API.IntegrationTests.Infrastructure;

[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<ApiFactory>;
