using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDbService;
using MongoDbTokenManager.Database;
using Xunit;

namespace MongoDbTokenManager.Tests;

/// <summary>
/// Registration only, so no server is needed: the driver connects lazily and the TTL index is
/// created on first use rather than in the constructor.
/// </summary>
public class ServiceRegistrationTests
{
    private static ServiceProvider BuildProvider(Action<IServiceCollection> register)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MongoDbSettings:ConnectionString", "mongodb://localhost:27017" },
                { "MongoDbSettings:DatabaseName", "TokenManagerRegistrationTests" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        // Registered directly rather than via AddMongoDbServices so the test needs no logging
        // provider, matching how ServiceGuardTests builds its MongoService.
        services.AddSingleton(new MongoService(configuration, NullLogger<MongoService>.Instance));
        register(services);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddMongoDbTokenServices_registers_the_concrete_service()
    {
        using var provider = BuildProvider(services => services.AddMongoDbTokenServices());

        Assert.NotNull(provider.GetRequiredService<MongoDbTokenService>());
    }

    /// <summary>
    /// Consumers are told to depend on the abstraction, so it has to resolve.
    /// </summary>
    [Fact]
    public void AddMongoDbTokenServices_registers_the_abstraction()
    {
        using var provider = BuildProvider(services => services.AddMongoDbTokenServices());

        Assert.NotNull(provider.GetRequiredService<AbstractTokenService>());
    }

    /// <summary>
    /// Both registrations must be the same object, or the TTL index gate and the pepper would be
    /// duplicated across two services pointed at one collection.
    /// </summary>
    [Fact]
    public void Both_registrations_resolve_to_one_instance()
    {
        using var provider = BuildProvider(services => services.AddMongoDbTokenServices());

        Assert.Same(
            provider.GetRequiredService<MongoDbTokenService>(),
            (object)provider.GetRequiredService<AbstractTokenService>());
    }

    /// <summary>
    /// The options overload must keep the abstraction registered. Configuring a pepper previously
    /// meant hand-rolling an AddSingleton for the concrete type, which quietly left
    /// AbstractTokenService unregistered and broke every consumer depending on it.
    /// </summary>
    [Fact]
    public void The_options_overload_still_registers_the_abstraction()
    {
        using var provider = BuildProvider(services => services.AddMongoDbTokenServices(
            cleanupAfterExpiry: TimeSpan.FromHours(1),
            hashPepper: "pepper-from-a-secret-store"));

        Assert.NotNull(provider.GetRequiredService<AbstractTokenService>());
        Assert.Same(
            provider.GetRequiredService<MongoDbTokenService>(),
            (object)provider.GetRequiredService<AbstractTokenService>());
    }

    /// <summary>
    /// The container fills a constructor's optional parameters from their defaults, so registering
    /// the type directly could never apply a pepper. Proven through the observable effect: a digest
    /// keyed with a pepper does not verify without it.
    /// </summary>
    [Fact]
    public void A_peppered_digest_does_not_validate_without_the_pepper()
    {
        const string salt = "user-1";
        const string token = "123456";
        var expiresAt = DateTime.UtcNow.AddMinutes(5);

        var peppered = new TokenValue(salt, token, "pepper-from-a-secret-store");

        Assert.True(peppered.Valid(salt, token, expiresAt, "pepper-from-a-secret-store"));
        Assert.False(peppered.Valid(salt, token, expiresAt));
        Assert.False(peppered.Valid(salt, token, expiresAt, "a-different-pepper"));
    }
}
