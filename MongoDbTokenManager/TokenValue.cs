using Common.Utilities;

namespace MongoDbTokenManager
{
    public sealed class TokenValue
    {
        public string OneTimeTokenHash { get; private set; }
        public DateTime ValidUntilUtc { get; private set; }

        public TokenValue(string salt, string oneTimeToken, int vaildityInSeconds)
        {
            this.OneTimeTokenHash = ComputeOneTimeToken(salt, oneTimeToken);
            ValidUntilUtc = DateTime.UtcNow.AddSeconds(vaildityInSeconds);
        }


        public bool Valid(string salt, string oneTimeToken)
        {
            return OneTimeTokenHash == ComputeOneTimeToken(salt, oneTimeToken) && DateTime.UtcNow <= ValidUntilUtc;
        }

        private string ComputeOneTimeToken(string salt, string oneTimeToken) => CryptoUtils.ComputeSha512Hash($"{salt.ToLowerInvariant()}####{oneTimeToken.ToLowerInvariant()}");
	}
}
