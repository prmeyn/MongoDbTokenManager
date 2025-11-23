using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDbService;
using MongoDbTokenManager.Database;
using Xunit;

namespace MongoDbTokenManager.Tests;

public class TokenServiceTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    public async Task GenerateAndValidateToken_Success(int numberOfDigits)
    {
        // Arrange
        // Use a connection string from environment variable or default to localhost for local testing
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";
        var databaseName = "TokenManagerTestDb_" + Guid.NewGuid();
        
        var myConfiguration = new Dictionary<string, string>
        {
            {"MongoDbSettings:ConnectionString", connectionString},
            {"MongoDbSettings:MongoDatabaseName", databaseName}
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
            var token = await tokenService.Generate(logId, tokenId, 300, numberOfDigits);

            // Assert
            Assert.NotNull(token);
            if (numberOfDigits > 0)
            {
                Assert.Equal(numberOfDigits, token.Length);
            }
            else
            {
                Assert.True(token.Length > 0); // GUID length varies but is > 0
            }

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

    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    public async Task GenerateToken_ExpiresAfterValidityPeriod(int numberOfDigits)
    {
        // Arrange
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";
        var databaseName = "TokenManagerTestDb_" + Guid.NewGuid();
        
        var myConfiguration = new Dictionary<string, string>
        {
            {"MongoDbSettings:ConnectionString", connectionString},
            {"MongoDbSettings:MongoDatabaseName", databaseName}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        var mongoService = new MongoService(configuration, NullLogger<MongoService>.Instance);
        var tokenService = new MongoDbTokenService(mongoService);
        var tokenId = new TokenIdentifier("test-user-expiration");
        var logId = "test-log-id-expiration";

        try
        {
            // Act
            var token = await tokenService.Generate(logId, tokenId, 1, numberOfDigits); // 1 second validity

            // Assert
            Assert.NotNull(token);
            
            // Wait for expiration
            await Task.Delay(2000);

            var isValid = await tokenService.Validate(tokenId, token);
            Assert.False(isValid, "Token should be invalid after expiration period");
        }
        finally
        {
            // Cleanup
            await mongoService.Database.Client.DropDatabaseAsync(databaseName);
        }
    }

    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    public async Task ValidateToken_FailsForInvalidToken_SucceedsForValidToken(int numberOfDigits)
    {
        // Arrange
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";
        var databaseName = "TokenManagerTestDb_" + Guid.NewGuid();
        
        var myConfiguration = new Dictionary<string, string>
        {
            {"MongoDbSettings:ConnectionString", connectionString},
            {"MongoDbSettings:MongoDatabaseName", databaseName}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        var mongoService = new MongoService(configuration, NullLogger<MongoService>.Instance);
        var tokenService = new MongoDbTokenService(mongoService);
        var tokenId = new TokenIdentifier("test-user-invalid-check");
        var logId = "test-log-id-invalid-check";

        try
        {
            // Act
            var token = await tokenService.Generate(logId, tokenId, 300, numberOfDigits);

            // Assert
            Assert.NotNull(token);

            // Try invalid token
            var invalidToken = "invalid-token";
            if (numberOfDigits > 0) 
            {
                invalidToken = new string('0', numberOfDigits);
                if (invalidToken == token) invalidToken = new string('1', numberOfDigits); // Ensure it's different
            }
            
            var isValidInvalid = await tokenService.Validate(tokenId, invalidToken);
            Assert.False(isValidInvalid, "Validation should fail for incorrect token");

            // Try valid token
            var isValid = await tokenService.Validate(tokenId, token);
            Assert.True(isValid, "Validation should succeed for correct token");
        }
        finally
        {
            // Cleanup
            await mongoService.Database.Client.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task ValidateToken_FailsAfterMaxAttempts()
    {
        // Arrange
        var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";
        var databaseName = "TokenManagerTestDb_" + Guid.NewGuid();
        
        var myConfiguration = new Dictionary<string, string>
        {
            {"MongoDbSettings:ConnectionString", connectionString},
            {"MongoDbSettings:MongoDatabaseName", databaseName}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        var mongoService = new MongoService(configuration, NullLogger<MongoService>.Instance);
        var tokenService = new MongoDbTokenService(mongoService);
        var tokenId = new TokenIdentifier("test-user-max-attempts");
        var logId = "test-log-id-max-attempts";

        try
        {
            // Act
            var token = await tokenService.Generate(logId, tokenId, 300, 6);

            // Assert
            Assert.NotNull(token);

            // Fail 5 times (MAXIMUM_ATTEMPTS)
            var invalidToken = "000000";
            if (invalidToken == token) invalidToken = "111111";

            for (int i = 0; i < 5; i++)
            {
                var isValidInvalid = await tokenService.Validate(tokenId, invalidToken);
                Assert.False(isValidInvalid, $"Attempt {i + 1} should fail");
            }

            // Try valid token - should fail because max attempts reached
            var isValid = await tokenService.Validate(tokenId, token);
            Assert.False(isValid, "Validation should fail after max attempts reached, even with correct token");
        }
        finally
        {
            // Cleanup
            await mongoService.Database.Client.DropDatabaseAsync(databaseName);
        }
    }
}
