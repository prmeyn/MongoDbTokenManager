using Xunit;

namespace MongoDbTokenManager.Tests;

public class TokenIdentifierTests
{
    [Theory]
    [InlineData("  User@Example.COM  ", "user@example.com")]
    [InlineData("abc", "abc")]
    public void Constructor_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, new TokenIdentifier(input).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlank(string input)
    {
        Assert.Throws<ArgumentException>(() => new TokenIdentifier(input));
    }

    [Fact]
    public void Constructor_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new TokenIdentifier(null!));
    }

    [Fact]
    public void Constructor_DoesNotLeakTheIdentifierIntoTheMessage()
    {
        var secret = "   ";
        var ex = Assert.Throws<ArgumentException>(() => new TokenIdentifier(secret));
        Assert.DoesNotContain(">>", ex.Message);
    }

    [Fact]
    public void GetHashCode_DoesNotThrowAndAgreesWithEquals()
    {
        var a = new TokenIdentifier("user-1");
        var b = new TokenIdentifier("USER-1");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void CanBeUsedAsADictionaryKey()
    {
        var map = new Dictionary<TokenIdentifier, int> { [new TokenIdentifier("user-1")] = 7 };

        Assert.Equal(7, map[new TokenIdentifier("user-1")]);
        Assert.True(map.ContainsKey("USER-1"));
    }

    [Fact]
    public void EqualsAndOperatorAgree()
    {
        var a = new TokenIdentifier("user-1");
        var b = new TokenIdentifier("user-2");

        Assert.True(a == new TokenIdentifier("user-1"));
        Assert.True(a.Equals(new TokenIdentifier("user-1")));
        Assert.True(a != b);
        Assert.False(a.Equals(b));
        Assert.False(a.Equals("not a token identifier"));
    }

    [Fact]
    public void CultureIgnorableCharactersAreNotTreatedAsEqual()
    {
        // A zero-width joiner is ignorable under culture-sensitive comparison, which would
        // have made these two distinct identifiers compare equal.
        var withJoiner = new TokenIdentifier("us‍er");
        var without = new TokenIdentifier("user");

        Assert.NotEqual(withJoiner, without);
        Assert.True(withJoiner != without);
    }

    [Fact]
    public void DefaultInstance_DoesNotThrow()
    {
        // A struct can always be default-constructed, which bypasses the validating ctor.
        var uninitialised = default(TokenIdentifier);

        Assert.Equal(string.Empty, uninitialised.ToString());
        Assert.Equal(string.Empty, (string)uninitialised);
        Assert.Equal(uninitialised, default(TokenIdentifier));
        uninitialised.GetHashCode();
    }

    [Fact]
    public void ImplicitConversionFromStringApplies()
    {
        TokenIdentifier id = "  Mixed-Case  ";
        Assert.Equal("mixed-case", id.ToString());
    }
}
