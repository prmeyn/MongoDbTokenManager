using MongoDB.Driver;
using MongoDbService;
using MongoDbTokenManager.Database.DTOs;

namespace MongoDbTokenManager.Database
{
	public sealed class MongoDbTokenService : AbstractTokenService
    {
        private IMongoCollection<Tokens> _tokenCollection;

		public MongoDbTokenService(
			MongoService mongoService,
			TimeSpan? cleanupAfterExpiry = null)
        {
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
            var isValid = await Validate(id, token);
            await Consume(id);
            return await Task.FromResult(isValid);
        }

        public override async Task<string> Generate(string logId, TokenIdentifier id, int validityInSeconds, int numberOfDigits = 0)
        {
            string oneTimeToken;
            var tokenInDb = await _tokenCollection.Find(Filter(id)).FirstOrDefaultAsync();
            if (tokenInDb is not null)
            {
                await Consume(id);
            }
            oneTimeToken = (numberOfDigits > 0) ? Utils.GetRandomNumber(numberOfDigits) : Guid.NewGuid().ToString().ToLowerInvariant();

            var idAsString = id.ToString();
            var filter = Builders<Tokens>.Filter.Eq(t => t.Id, idAsString);
            var options = new ReplaceOptions { IsUpsert = true };
            var tokenValue = new TokenValue(salt: idAsString, oneTimeToken);
            var expiresAt = DateTime.UtcNow.AddSeconds(validityInSeconds);
            await _tokenCollection.ReplaceOneAsync(filter, new Tokens() { LogId = logId, Id = idAsString, Token = tokenValue, ExpiresAt = expiresAt }, options);
            return await Task.FromResult(oneTimeToken);
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

			if (tokenInDb == null) // Null check added
			{
				return false;
			}

			return await Task.FromResult(tokenInDb?.Token?.Valid(salt: idAsString, token, tokenInDb.ExpiresAt) ?? false);
        }
    }
}
