using Common.Utilities;

namespace MongoDbTokenManager
{
    public sealed class TokenValue
    {
        public string OneTimeTokenHash { get; private set; }
        public DateTime ValidUntilUtc { get; private set; }

        public TokenValue(string oneTimeToken, int vaildityInSeconds)
        {
            this.OneTimeTokenHash = ComputeOneTimeToken(oneTimeToken);
            ValidUntilUtc = DateTime.UtcNow.AddSeconds(vaildityInSeconds);
        }


        public bool Valid(string oneTimeToken)
        {
            return OneTimeTokenHash == ComputeOneTimeToken(oneTimeToken) && DateTime.UtcNow <= ValidUntilUtc;
        }

        private string ComputeOneTimeToken(string oneTimeToken) => CryptoUtils.ComputeSha512Hash(oneTimeToken.ToLowerInvariant());
	}
}
