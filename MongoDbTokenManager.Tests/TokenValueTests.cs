using Xunit;

namespace MongoDbTokenManager.Tests;

public class TokenValueTests
{
    private static readonly DateTime NotExpired = DateTime.UtcNow.AddMinutes(5);

    [Fact]
    public void Valid_TrueForTheMatchingToken()
    {
        var value = new TokenValue(salt: "user-1", "123456");

        Assert.True(value.Valid(salt: "user-1", "123456", NotExpired));
    }

    [Theory]
    [InlineData("654321")]
    [InlineData("")]
    [InlineData("12345")]
    public void Valid_FalseForAnyOtherToken(string candidate)
    {
        var value = new TokenValue(salt: "user-1", "123456");

        Assert.False(value.Valid(salt: "user-1", candidate, NotExpired));
    }

    [Fact]
    public void Valid_FalseForADifferentSalt()
    {
        var value = new TokenValue(salt: "user-1", "123456");

        Assert.False(value.Valid(salt: "user-2", "123456", NotExpired));
    }

    [Fact]
    public void Valid_FalseOnceExpired()
    {
        var value = new TokenValue(salt: "user-1", "123456");

        Assert.False(value.Valid(salt: "user-1", "123456", DateTime.UtcNow.AddSeconds(-1)));
    }

    [Fact]
    public void HashDoesNotContainTheToken()
    {
        var value = new TokenValue(salt: "user-1", "123456");

        Assert.DoesNotContain("123456", value.OneTimeTokenHash);
    }

    [Fact]
    public void Pepper_ChangesTheStoredHash()
    {
        var unpeppered = new TokenValue(salt: "user-1", "123456");
        var peppered = new TokenValue(salt: "user-1", "123456", pepper: "server-secret");

        Assert.NotEqual(unpeppered.OneTimeTokenHash, peppered.OneTimeTokenHash);
    }

    [Fact]
    public void Pepper_MustMatchToValidate()
    {
        var value = new TokenValue(salt: "user-1", "123456", pepper: "server-secret");

        Assert.True(value.Valid(salt: "user-1", "123456", NotExpired, pepper: "server-secret"));
        Assert.False(value.Valid(salt: "user-1", "123456", NotExpired, pepper: "wrong-secret"));
        Assert.False(value.Valid(salt: "user-1", "123456", NotExpired));
    }

    [Fact]
    public void NoPepper_KeepsTheOriginalHashFormat()
    {
        // Guards the upgrade path: existing stored hashes must still validate when no pepper
        // is configured, so adopting this version does not invalidate tokens in flight.
        var value = new TokenValue(salt: "user-1", "123456");

        Assert.Equal(128, value.OneTimeTokenHash.Length);
        Assert.True(value.Valid(salt: "user-1", "123456", NotExpired, pepper: null));
    }
}
