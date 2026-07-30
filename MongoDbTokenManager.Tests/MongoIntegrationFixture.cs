using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDbService;
using MongoDbTokenManager.Database;

namespace MongoDbTokenManager.Tests;

/// <summary>
/// Per-test MongoDB scope: builds a <see cref="MongoDbTokenService"/> against a uniquely
/// named database and drops it on disposal, so tests cannot interfere with one another.
/// Requires a reachable server; set MONGODB_CONNECTION_STRING to point somewhere other than
/// localhost.
/// </summary>
internal sealed class MongoIntegrationFixture : IAsyncDisposable
{
    private readonly MongoService _mongoService;
    private readonly string _databaseName;

    public MongoDbTokenService TokenService { get; }

    public MongoIntegrationFixture(TimeSpan? cleanupAfterExpiry = null, string? hashPepper = null)
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";
        _databaseName = "TokenManagerTestDb_" + Guid.NewGuid();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MongoDbSettings:ConnectionString", connectionString },
                { "MongoDbSettings:DatabaseName", _databaseName }
            })
            .Build();

        _mongoService = new MongoService(configuration, NullLogger<MongoService>.Instance);
        TokenService = new MongoDbTokenService(_mongoService, cleanupAfterExpiry, hashPepper);
    }

    public async ValueTask DisposeAsync()
    {
        // Deliberately not passing TestContext.Current.CancellationToken: cleanup must still
        // run when a test is cancelled or times out, otherwise the database is left behind.
        await _mongoService.Database.Client.DropDatabaseAsync(_databaseName);
    }
}
