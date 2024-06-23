namespace MongoDbTokenManager
{
    public sealed class TokenValue
    {
        public string OneTimeToken { get; private set; }
        public DateTime ValidUntilUtc { get; private set; }

        public TokenValue(string oneTimeToken, int vaildityInSeconds)
        {
            this.OneTimeToken = oneTimeToken;
            ValidUntilUtc = DateTime.UtcNow.AddSeconds(vaildityInSeconds);
        }
        public TokenValue(string oneTimeToken, DateTime validUntilUTC)
        {
            this.OneTimeToken = oneTimeToken;
            ValidUntilUtc = validUntilUTC;
        }


        public bool Valid(string oneTimeToken)
        {
            return this.OneTimeToken.ToLowerInvariant() == oneTimeToken.ToLowerInvariant() && DateTime.UtcNow <= ValidUntilUtc;
        }
    }
}
