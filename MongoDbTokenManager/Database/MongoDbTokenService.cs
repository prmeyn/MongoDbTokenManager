using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbService;
using MongoDbTokenManager.Database.DTOs;

namespace MongoDbTokenManager.Database
{
	public sealed class MongoDbTokenService : AbstractTokenService
    {
        private const int IndexOptionsConflictErrorCode = 85;
        private const string TtlIndexName = "ExpiresAt_ttl";

        private readonly IMongoCollection<Tokens> _tokenCollection;
        private readonly string? _hashPepper;
        private readonly TimeSpan _ttlExpiry;
        private readonly SemaphoreSlim _ttlIndexGate = new(1, 1);
        private volatile bool _ttlIndexReady;

		public MongoDbTokenService(
			MongoService mongoService,
			TimeSpan? cleanupAfterExpiry = null,
			string? hashPepper = null)
        {
            _hashPepper = hashPepper;
            _ttlExpiry = cleanupAfterExpiry ?? TimeSpan.FromHours(24);
            _tokenCollection = mongoService.Database.GetCollection<Tokens>(nameof(Tokens), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });
        }

        /// <summary>
        /// Creates the TTL index on first use. Deferred out of the constructor so that
        /// building the DI container does not block on a network round trip, and so an
        /// unreachable database surfaces on the call that needs it rather than at startup.
        /// A failed attempt is not cached, so a transient outage does not disable the service.
        /// </summary>
        private async Task EnsureTtlIndex()
        {
            if (_ttlIndexReady)
            {
                return;
            }

            await _ttlIndexGate.WaitAsync();
            try
            {
                if (_ttlIndexReady)
                {
                    return;
                }

                var indexModel = new CreateIndexModel<Tokens>(
                    Builders<Tokens>.IndexKeys.Ascending(t => t.ExpiresAt),
                    new CreateIndexOptions { Name = TtlIndexName, ExpireAfter = _ttlExpiry }
                );

                try
                {
                    await _tokenCollection.Indexes.CreateOneAsync(indexModel);
                }
                catch (MongoCommandException e) when (e.Code == IndexOptionsConflictErrorCode)
                {
                    // The index exists with a different expireAfterSeconds. MongoDB refuses to
                    // recreate it, so amend it in place instead. Without this, passing a
                    // cleanupAfterExpiry that differs from the one the collection was created
                    // with - which README documents doing - would throw on every call.
                    await _tokenCollection.Database.RunCommandAsync<BsonDocument>(new BsonDocument
                    {
                        { "collMod", _tokenCollection.CollectionNamespace.CollectionName },
                        { "index", new BsonDocument
                            {
                                { "name", TtlIndexName },
                                { "expireAfterSeconds", _ttlExpiry.TotalSeconds }
                            }
                        }
                    });
                }

                _ttlIndexReady = true;
            }
            finally
            {
                _ttlIndexGate.Release();
            }
        }

        private FilterDefinition<Tokens> Filter(TokenIdentifier id) => Builders<Tokens>.Filter.Eq(t => t.Id, id.ToString());

        public override async Task Consume(TokenIdentifier id)
        {
            await EnsureTtlIndex();
            await _tokenCollection.DeleteOneAsync(Filter(id));
        }

        public override async Task<bool> ConsumeAndValidate(TokenIdentifier id, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            await EnsureTtlIndex();

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

            await EnsureTtlIndex();

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

            await EnsureTtlIndex();

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
