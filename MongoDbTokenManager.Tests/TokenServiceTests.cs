using MongoDB.Driver;
using MongoDbTokenManager.Database.DTOs;
using Xunit;

namespace MongoDbTokenManager.Tests;

public class TokenServiceTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    public async Task GenerateAndValidateToken_Success(int numberOfDigits)
    {
        await using var fixture = new MongoIntegrationFixture();
        var tokenId = new TokenIdentifier("test-user");

        var token = await fixture.TokenService.Generate("test-log-id", tokenId, 300, numberOfDigits);

        Assert.NotNull(token);
        if (numberOfDigits > 0)
        {
            Assert.Equal(numberOfDigits, token.Length);
        }
        else
        {
            Assert.True(token.Length > 0); // GUID length varies but is > 0
        }

        Assert.True(await fixture.TokenService.Validate(tokenId, token));
        Assert.True(await fixture.TokenService.ConsumeAndValidate(tokenId, token));
        Assert.False(await fixture.TokenService.Validate(tokenId, token));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    public async Task GenerateToken_ExpiresAfterValidityPeriod(int numberOfDigits)
    {
        await using var fixture = new MongoIntegrationFixture();
        var tokenId = new TokenIdentifier("test-user-expiration");

        var token = await fixture.TokenService.Generate("test-log-id-expiration", tokenId, 1, numberOfDigits);
        Assert.NotNull(token);

        await Task.Delay(2000, TestContext.Current.CancellationToken);

        Assert.False(await fixture.TokenService.Validate(tokenId, token), "Token should be invalid after expiration period");
    }

    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    public async Task ValidateToken_FailsForInvalidToken_SucceedsForValidToken(int numberOfDigits)
    {
        await using var fixture = new MongoIntegrationFixture();
        var tokenId = new TokenIdentifier("test-user-invalid-check");

        var token = await fixture.TokenService.Generate("test-log-id-invalid-check", tokenId, 300, numberOfDigits);
        Assert.NotNull(token);

        var invalidToken = "invalid-token";
        if (numberOfDigits > 0)
        {
            invalidToken = new string('0', numberOfDigits);
            if (invalidToken == token) invalidToken = new string('1', numberOfDigits); // Ensure it's different
        }

        Assert.False(await fixture.TokenService.Validate(tokenId, invalidToken), "Validation should fail for incorrect token");
        Assert.True(await fixture.TokenService.Validate(tokenId, token), "Validation should succeed for correct token");
    }

    [Fact]
    public async Task ConsumeAndValidate_ValidatesAndRemovesToken()
    {
        await using var fixture = new MongoIntegrationFixture();
        var tokenId = new TokenIdentifier("test-user-consume");

        var token = await fixture.TokenService.Generate("test-log-id-consume", tokenId, 300, 6);
        Assert.NotNull(token);

        Assert.True(await fixture.TokenService.ConsumeAndValidate(tokenId, token), "ConsumeAndValidate should return true for valid token");
        Assert.False(await fixture.TokenService.Validate(tokenId, token), "Token should be invalid after being consumed");
    }

    [Fact]
    public async Task ConsumeAndValidate_KeepsTheTokenWhenTheGuessIsWrong()
    {
        await using var fixture = new MongoIntegrationFixture();
        var tokenId = new TokenIdentifier("test-user-wrong-guess");

        var token = await fixture.TokenService.Generate("test-log-id-wrong-guess", tokenId, 300, 6);

        Assert.False(await fixture.TokenService.ConsumeAndValidate(tokenId, "000000" == token ? "111111" : "000000"));
        Assert.True(await fixture.TokenService.Validate(tokenId, token), "a wrong guess must not discard the pending token");
        Assert.True(await fixture.TokenService.ConsumeAndValidate(tokenId, token), "the correct token still works after a wrong guess");
    }

    [Fact]
    public async Task ConsumeAndValidate_OnlyOneOfTwoConcurrentCallersSucceeds()
    {
        await using var fixture = new MongoIntegrationFixture();
        var tokenId = new TokenIdentifier("test-user-concurrent");

        var token = await fixture.TokenService.Generate("test-log-id-concurrent", tokenId, 300, 6);

        var results = await Task.WhenAll(
            fixture.TokenService.ConsumeAndValidate(tokenId, token),
            fixture.TokenService.ConsumeAndValidate(tokenId, token));

        Assert.Single(results, true);
    }

    [Fact]
    public async Task ConsumeAndValidate_FalseForAnExpiredToken()
    {
        await using var fixture = new MongoIntegrationFixture();
        var tokenId = new TokenIdentifier("test-user-expired-consume");

        var token = await fixture.TokenService.Generate("test-log-id-expired-consume", tokenId, 1, 6);
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        Assert.False(await fixture.TokenService.ConsumeAndValidate(tokenId, token));
    }

    [Fact]
    public async Task Consume_RemovesTheToken()
    {
        await using var fixture = new MongoIntegrationFixture();
        var tokenId = new TokenIdentifier("test-user-consume-only");

        var token = await fixture.TokenService.Generate("test-log-id-consume-only", tokenId, 300, 6);
        Assert.True(await fixture.TokenService.Validate(tokenId, token));

        await fixture.TokenService.Consume(tokenId);

        Assert.False(await fixture.TokenService.Validate(tokenId, token));
    }

    [Fact]
    public async Task Generate_ReplacesAnyPreviousToken()
    {
        await using var fixture = new MongoIntegrationFixture();
        var tokenId = new TokenIdentifier("test-user-regenerate");

        var first = await fixture.TokenService.Generate("test-log-id-regenerate", tokenId, 300, 6);
        var second = await fixture.TokenService.Generate("test-log-id-regenerate", tokenId, 300, 6);

        Assert.False(await fixture.TokenService.Validate(tokenId, first), "the superseded token should no longer validate");
        Assert.True(await fixture.TokenService.Validate(tokenId, second));
    }

    [Fact]
    public async Task PepperedTokens_ValidateOnlyWithTheSamePepper()
    {
        await using var fixture = new MongoIntegrationFixture(hashPepper: "server-secret");
        var tokenId = new TokenIdentifier("test-user-pepper");

        var token = await fixture.TokenService.Generate("test-log-id-pepper", tokenId, 300, 6);

        Assert.True(await fixture.TokenService.Validate(tokenId, token));
    }

    [Theory]
    [InlineData("ExpiresAt_1")]   // the name every release up to 10.1.0 produced
    [InlineData("ExpiresAt_ttl")] // the name 10.2.0 produced
    public async Task UpgradesOverAnExistingTtlIndexWhateverItIsCalled(string legacyIndexName)
    {
        // 10.2.0 asked for the ExpiresAt index under a new name and then ran collMod against
        // that assumed name, so upgrading a database created by an earlier release threw on
        // every call. Plant the legacy index with a different TTL and check the service copes.
        await using var fixture = new MongoIntegrationFixture(cleanupAfterExpiry: TimeSpan.FromHours(1));
        var collection = fixture.Database.GetCollection<Tokens>(nameof(Tokens));

        await collection.Indexes.CreateOneAsync(new CreateIndexModel<Tokens>(
            Builders<Tokens>.IndexKeys.Ascending(t => t.ExpiresAt),
            new CreateIndexOptions { Name = legacyIndexName, ExpireAfter = TimeSpan.FromHours(72) }),
            cancellationToken: TestContext.Current.CancellationToken);

        var tokenId = new TokenIdentifier("test-user-legacy-index");
        var token = await fixture.TokenService.Generate("test-log-id-legacy-index", tokenId, 300, 6);

        Assert.True(await fixture.TokenService.Validate(tokenId, token));

        // The existing index was amended in place, not duplicated.
        using var cursor = await collection.Indexes.ListAsync(TestContext.Current.CancellationToken);
        var indexes = await cursor.ToListAsync(TestContext.Current.CancellationToken);
        var onExpiresAt = indexes.Where(i => i["key"].AsBsonDocument.Contains(nameof(Tokens.ExpiresAt))).ToList();

        Assert.Single(onExpiresAt);
        Assert.Equal(legacyIndexName, onExpiresAt[0]["name"].AsString);
        Assert.Equal(3600, onExpiresAt[0]["expireAfterSeconds"].ToDouble());
    }

    [Fact]
    public async Task CleanupAfterExpiry_CanBeChangedOnAnExistingCollection()
    {
        // MongoDB raises IndexOptionsConflict when an index is recreated with different
        // options, so a second service with a different TTL used to throw on every call.
        await using var fixture = new MongoIntegrationFixture(cleanupAfterExpiry: TimeSpan.FromHours(24));
        var tokenId = new TokenIdentifier("test-user-ttl");

        await fixture.TokenService.Generate("test-log-id-ttl", tokenId, 300, 6);

        var withDifferentTtl = new MongoIntegrationFixture(cleanupAfterExpiry: TimeSpan.FromHours(1));
        await using (withDifferentTtl)
        {
            var token = await withDifferentTtl.TokenService.Generate("test-log-id-ttl-2", tokenId, 300, 6);
            Assert.True(await withDifferentTtl.TokenService.Validate(tokenId, token));
        }
    }
}
