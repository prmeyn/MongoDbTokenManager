using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDbService;
using MongoDbTokenManager.Database;
using Xunit;

namespace MongoDbTokenManager.Tests;

/// <summary>
/// Argument guards run before the service touches MongoDB, so these need no server. The
/// connection string is never dialled: the driver connects lazily and the TTL index is
/// created on first use, not in the constructor.
/// </summary>
public class ServiceGuardTests
{
    private static MongoDbTokenService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MongoDbSettings:ConnectionString", "mongodb://localhost:27017" },
                { "MongoDbSettings:DatabaseName", "TokenManagerGuardTests" }
            })
            .Build();

        return new MongoDbTokenService(new MongoService(configuration, NullLogger<MongoService>.Instance));
    }

    [Fact]
    public async Task Generate_RejectsAnUninitialisedIdentifier()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => CreateService().Generate("log", default, 300, 6));
        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public async Task Validate_RejectsAnUninitialisedIdentifier()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().Validate(default, "123456"));
    }

    [Fact]
    public async Task ConsumeAndValidate_RejectsAnUninitialisedIdentifier()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().ConsumeAndValidate(default, "123456"));
    }

    [Fact]
    public async Task Consume_RejectsAnUninitialisedIdentifier()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().Consume(default));
    }

    [Theory]
    [InlineData(-1, 300)]
    [InlineData(6, 0)]
    [InlineData(6, -5)]
    public async Task Generate_RejectsOutOfRangeArguments(int numberOfDigits, int validityInSeconds)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateService().Generate("log", new TokenIdentifier("user-1"), validityInSeconds, numberOfDigits));
    }

    [Fact]
    public void IsEmpty_TrueOnlyForTheDefaultInstance()
    {
        Assert.True(default(TokenIdentifier).IsEmpty);
        Assert.False(new TokenIdentifier("user-1").IsEmpty);
    }
}
