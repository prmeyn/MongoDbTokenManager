using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDbService;
using MongoDbTokenManager.Database;
using Xunit;

namespace MongoDbTokenManager.Tests;

public class TokenServiceTests
{
    [Fact]
    public async Task GenerateAndValidateToken_Success()
    {
        // Arrange
        // Use a connection string from environment variable or default to localhost for local testing
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";
        var databaseName = "TokenManagerTestDb_" + Guid.NewGuid();
        
        var myConfiguration = new Dictionary<string, string>
        {
            {"MongoDbSettings:ConnectionString", connectionString},
            {"MongoDbSettings:DatabaseName", databaseName}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        var mongoService = new MongoService(configuration, NullLogger<MongoService>.Instance);

        var tokenService = new MongoDbTokenService(mongoService);
        var tokenId = new TokenIdentifier("test-user");
        var logId = "test-log-id";

        try
        {
            // Act
            var token = await tokenService.Generate(logId, tokenId, 300, 6);

            // Assert
            Assert.NotNull(token);
            Assert.Equal(6, token.Length);

            var isValid = await tokenService.Validate(tokenId, token);
            Assert.True(isValid);

            var isConsumed = await tokenService.ConsumeAndValidate(tokenId, token);
            Assert.True(isConsumed);

            var isValidAfterConsume = await tokenService.Validate(tokenId, token);
            Assert.False(isValidAfterConsume);
        }
        finally
        {
            // Cleanup
            await mongoService.Database.Client.DropDatabaseAsync(databaseName);
        }
    }
}
