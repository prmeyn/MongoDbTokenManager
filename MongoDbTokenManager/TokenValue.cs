using Meyn.Utilities;
using System.Security.Cryptography;
using System.Text;

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
            return HashesMatch(OneTimeTokenHash, ComputeOneTimeToken(salt, oneTimeToken)) && DateTime.UtcNow <= expiresAt;
        }

        private string ComputeOneTimeToken(string salt, string oneTimeToken) => CryptoUtils.ComputeSha512Hash($"{salt.ToLowerInvariant()}####{oneTimeToken.ToLowerInvariant()}");

        private static bool HashesMatch(string stored, string candidate) =>
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(candidate));
	}
}
