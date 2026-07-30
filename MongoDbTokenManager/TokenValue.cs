using Meyn.Utilities;
using System.Security.Cryptography;
using System.Text;

namespace MongoDbTokenManager
{
    public sealed class TokenValue
    {
        public string OneTimeTokenHash { get; private set; }

        public TokenValue(string salt, string oneTimeToken, string? pepper = null)
        {
            this.OneTimeTokenHash = ComputeOneTimeToken(salt, oneTimeToken, pepper);
        }

        public bool Valid(string salt, string oneTimeToken, DateTime expiresAt, string? pepper = null)
        {
            return HashesMatch(OneTimeTokenHash, ComputeOneTimeToken(salt, oneTimeToken, pepper)) && DateTime.UtcNow <= expiresAt;
        }

        private static string ComputeOneTimeToken(string salt, string oneTimeToken, string? pepper)
        {
            var payload = $"{salt.ToLowerInvariant()}####{oneTimeToken.ToLowerInvariant()}";

            // Without a pepper the digest is a single SHA-512 pass over a known salt, so a
            // leaked collection lets a short numeric token be recovered by enumeration. Keying
            // the digest with a secret the database does not hold makes that infeasible.
            if (string.IsNullOrEmpty(pepper))
            {
                return CryptoUtils.ComputeSha512Hash(payload);
            }

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(pepper));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }

        private static bool HashesMatch(string stored, string candidate) =>
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(candidate));
	}
}
