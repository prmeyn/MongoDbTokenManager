using Common.Utilities;

namespace MongoDbTokenManager
{
    public sealed class TokenValue
    {
        public string OneTimeTokenHash { get; private set; }

        public TokenValue(string salt, string oneTimeToken)
        {
            this.OneTimeTokenHash = ComputeOneTimeToken(salt, oneTimeToken);
        }

        public bool Valid(string salt, string oneTimeToken, DateTime expiresAt)
        {
            return OneTimeTokenHash == ComputeOneTimeToken(salt, oneTimeToken) && DateTime.UtcNow <= expiresAt;
        }

        private string ComputeOneTimeToken(string salt, string oneTimeToken) => CryptoUtils.ComputeSha512Hash($"{salt.ToLowerInvariant()}####{oneTimeToken.ToLowerInvariant()}");
	}
}
