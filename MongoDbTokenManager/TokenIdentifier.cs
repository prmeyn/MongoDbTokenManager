namespace MongoDbTokenManager
{
    public struct TokenIdentifier
    {
        private string value;

        public TokenIdentifier(string value)
        {
            this.value = value.ToLowerInvariant().Trim();
            if (string.IsNullOrWhiteSpace(this.value)) { throw new Exception($"Invalid ID>>{value}<<"); }
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
            return this.value.Equals(other.value, StringComparison.InvariantCulture);
        }

        public override string ToString()
        {
            return this.value;
        }

        public static implicit operator TokenIdentifier(string value)
        {
            return new TokenIdentifier(value);
        }

        public static explicit operator string(TokenIdentifier tokenIdentifier)
        {
            return tokenIdentifier.value;
        }


        public static bool operator ==(TokenIdentifier left, TokenIdentifier right)
        {
            return left.value == right.value;
        }

        public static bool operator !=(TokenIdentifier left, TokenIdentifier right)
        {
            return !(left == right);
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
