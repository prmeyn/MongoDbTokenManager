using Xunit;

namespace MongoDbTokenManager.Tests;

public class GenerateCodeTests
{
    /// <summary>
    /// Exercises AbstractTokenService.GenerateCode without a database by stubbing the one
    /// abstract member it depends on.
    /// </summary>
    private sealed class StubTokenService(string codeToReturn) : AbstractTokenService
    {
        public override Task Consume(TokenIdentifier id) => Task.CompletedTask;
        public override Task<bool> ConsumeAndValidate(TokenIdentifier id, string token) => Task.FromResult(true);
        public override Task<bool> Validate(TokenIdentifier id, string token) => Task.FromResult(true);
        public override Task<string> Generate(string logId, TokenIdentifier id, int validityInSeconds, int numberOfDigits = 0) => Task.FromResult(codeToReturn);
    }

    [Fact]
    public async Task GenerateCode_ReturnsTheGeneratedCode()
    {
        var service = new StubTokenService("123456");

        var result = await service.GenerateCode("log", new TokenIdentifier("user-1"), 300, "https://example.com/verify/");

        Assert.Equal("123456", result.Code);
        Assert.Equal("https://example.com/verify/123456/user-1", result.QrCodeRelativeUrl);
    }

    [Fact]
    public async Task GenerateCode_EscapesAnIdentifierContainingPlus()
    {
        // An unescaped + decodes to a space on the receiving end, so the link resolves to a
        // different identifier than the one the token was issued for.
        var service = new StubTokenService("123456");

        var result = await service.GenerateCode("log", new TokenIdentifier("user+tag@example.com"), 300, "https://example.com/verify/");

        Assert.DoesNotContain("+", result.QrCodeRelativeUrl);
        Assert.Contains("user%2Btag%40example.com", result.QrCodeRelativeUrl);
    }

    [Theory]
    [InlineData("with space")]
    [InlineData("with#hash")]
    [InlineData("with?query")]
    [InlineData("with/slash")]
    public async Task GenerateCode_EscapesReservedCharacters(string identifier)
    {
        var service = new StubTokenService("123456");

        var result = await service.GenerateCode("log", identifier, 300, "https://example.com/verify/");

        var tail = result.QrCodeRelativeUrl["https://example.com/verify/123456/".Length..];
        Assert.Equal(Uri.EscapeDataString(identifier.ToLowerInvariant()), tail);
    }
}
