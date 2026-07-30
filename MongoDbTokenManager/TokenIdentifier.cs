namespace MongoDbTokenManager
{
    public readonly struct TokenIdentifier : IEquatable<TokenIdentifier>
    {
        private readonly string value;

        /// <summary>
        /// True for <c>default(TokenIdentifier)</c>, which bypasses the validating constructor
        /// because a struct can always be default-initialised.
        /// </summary>
        public bool IsEmpty => string.IsNullOrEmpty(value);

        public TokenIdentifier(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            this.value = value.ToLowerInvariant().Trim();
            if (string.IsNullOrWhiteSpace(this.value)) { throw new ArgumentException("The token identifier must not be blank.", nameof(value)); }
        }

        public override bool Equals(object? obj)
        {
            if (obj is TokenIdentifier tokenIdentifier)
            {
                return this.Equals(tokenIdentifier);
            }

            return false;
        }

        public bool Equals(TokenIdentifier other)
        {
            return string.Equals(this.value, other.value, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return this.value ?? string.Empty;
        }

        public static implicit operator TokenIdentifier(string value)
        {
            return new TokenIdentifier(value);
        }

        public static explicit operator string(TokenIdentifier tokenIdentifier)
        {
            return tokenIdentifier.ToString();
        }


        public static bool operator ==(TokenIdentifier left, TokenIdentifier right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TokenIdentifier left, TokenIdentifier right)
        {
            return !(left == right);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(this.ToString());
        }
    }
}
