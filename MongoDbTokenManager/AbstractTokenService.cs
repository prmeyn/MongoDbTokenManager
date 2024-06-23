namespace MongoDbTokenManager
{
	public abstract class AbstractTokenService
    {
        public abstract Task Consume(TokenIdentifier id);
        public abstract Task<bool> ConsumeAndValidate(TokenIdentifier id, string token);
        public abstract Task<string> Generate(string logId, TokenIdentifier id, int validityInSeconds, int numberOfDigits = 0);
        public abstract Task<bool> Validate(TokenIdentifier id, string token);

        public async Task<GeneratedCode> GenerateCode(string logId, TokenIdentifier id, int validityInSeconds, string relativeUrl, int numberOfDigits = 0)
        {
            var code = await Generate(logId, id, validityInSeconds);
            return await Task.FromResult<GeneratedCode>(new GeneratedCode(Code: code, QrCodeRelativeUrl: $"{relativeUrl}{code}/{id}"));
        }
    }
}
