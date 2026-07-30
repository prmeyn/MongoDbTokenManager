using MongoDB.Driver;
using MongoDbService;
using MongoDbTokenManager.Database.DTOs;

namespace MongoDbTokenManager.Database
{
	public sealed class MongoDbTokenService : AbstractTokenService
    {
        private readonly IMongoCollection<Tokens> _tokenCollection;
        private readonly string? _hashPepper;

		public MongoDbTokenService(
			MongoService mongoService,
			TimeSpan? cleanupAfterExpiry = null,
			string? hashPepper = null)
        {
            _hashPepper = hashPepper;
            _tokenCollection = mongoService.Database.GetCollection<Tokens>(nameof(Tokens), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });

            var ttlExpiry = cleanupAfterExpiry ?? TimeSpan.FromHours(24);
            var indexKeysDefinition = Builders<Tokens>.IndexKeys.Ascending(t => t.ExpiresAt);
            var indexModel = new CreateIndexModel<Tokens>(
                indexKeysDefinition,
                new CreateIndexOptions { ExpireAfter = ttlExpiry }
            );
            _tokenCollection.Indexes.CreateOne(indexModel);
        }

        private FilterDefinition<Tokens> Filter(TokenIdentifier id) => Builders<Tokens>.Filter.Eq(t => t.Id, id.ToString());

        public override async Task Consume(TokenIdentifier id)
        {
            await _tokenCollection.DeleteOneAsync(Filter(id));
        }

        public override async Task<bool> ConsumeAndValidate(TokenIdentifier id, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            // Fetch and delete in one server round trip. Reading and then deleting let two
            // concurrent callers both observe the same token as valid before either removed
            // it, which is exactly what a one-time token must not allow.
            var idAsString = id.ToString();
            var tokenInDb = await _tokenCollection.FindOneAndDeleteAsync(Builders<Tokens>.Filter.Eq(t => t.Id, idAsString));

            return tokenInDb?.Token.Valid(salt: idAsString, token, tokenInDb.ExpiresAt, _hashPepper) ?? false;
        }

        public override async Task<string> Generate(string logId, TokenIdentifier id, int validityInSeconds, int numberOfDigits = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(numberOfDigits);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(validityInSeconds);

            // No need to look for and delete an existing token first: the upsert below
            // replaces it in a single round trip, and deleting it beforehand left a window
            // in which a concurrent Validate saw no token at all.
            var oneTimeToken = (numberOfDigits > 0) ? Utils.GetRandomNumber(numberOfDigits) : Guid.NewGuid().ToString().ToLowerInvariant();

            var idAsString = id.ToString();
            var filter = Builders<Tokens>.Filter.Eq(t => t.Id, idAsString);
            var options = new ReplaceOptions { IsUpsert = true };
            var tokenValue = new TokenValue(salt: idAsString, oneTimeToken, _hashPepper);
            var expiresAt = DateTime.UtcNow.AddSeconds(validityInSeconds);
            await _tokenCollection.ReplaceOneAsync(filter, new Tokens() { LogId = logId, Id = idAsString, Token = tokenValue, ExpiresAt = expiresAt }, options);
            return oneTimeToken;
        }

        public override async Task<bool> Validate(TokenIdentifier id, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }
			var idAsString = id.ToString();
			var filter = Builders<Tokens>.Filter.Eq(t => t.Id, idAsString);

			var tokenInDb = await _tokenCollection.Find(filter).FirstOrDefaultAsync();

			if (tokenInDb is null)
			{
				return false;
			}

			return tokenInDb.Token.Valid(salt: idAsString, token, tokenInDb.ExpiresAt, _hashPepper);
        }
    }
}
