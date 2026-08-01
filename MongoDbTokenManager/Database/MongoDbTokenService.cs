using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbService;
using MongoDbTokenManager.Database.DTOs;

namespace MongoDbTokenManager.Database
{
	public sealed class MongoDbTokenService : AbstractTokenService
    {
        private const int IndexOptionsConflictErrorCode = 85;
        private const int IndexKeySpecsConflictErrorCode = 86;

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

                // Deliberately unnamed, so the driver derives "ExpiresAt_1" - the name every
                // release up to 10.1.0 produced. Requesting the same key pattern under any
                // other name is a conflict on a collection that already has this index.
                var indexModel = new CreateIndexModel<Tokens>(
                    Builders<Tokens>.IndexKeys.Ascending(t => t.ExpiresAt),
                    new CreateIndexOptions { ExpireAfter = _ttlExpiry }
                );

                try
                {
                    await _tokenCollection.Indexes.CreateOneAsync(indexModel);
                }
                catch (MongoCommandException e) when (e.Code is IndexOptionsConflictErrorCode or IndexKeySpecsConflictErrorCode)
                {
                    // An index on ExpiresAt already exists with different options. MongoDB
                    // refuses to recreate it, so amend it in place. Without this, passing a
                    // cleanupAfterExpiry that differs from the one the collection was created
                    // with - which README documents doing - would throw on every call.
                    await AmendTtlOnExistingIndex();
                }

                _ttlIndexReady = true;
            }
            finally
            {
                _ttlIndexGate.Release();
            }
        }

        /// <summary>
        /// Points collMod at whatever the existing ExpiresAt index is actually called. The name
        /// cannot be assumed: releases up to 10.1.0 created it unnamed, so the server called it
        /// "ExpiresAt_1", while 10.2.0 named it "ExpiresAt_ttl". collMod addresses an index by
        /// name and fails on a name that is not there, so guessing wrong turned every call into
        /// an exception for anyone upgrading an existing database.
        /// </summary>
        private async Task AmendTtlOnExistingIndex()
        {
            using var cursor = await _tokenCollection.Indexes.ListAsync();
            var indexes = await cursor.ToListAsync();

            var existing = indexes.FirstOrDefault(index =>
                index.TryGetValue("key", out var key)
                && key is BsonDocument keyDocument
                && keyDocument.ElementCount == 1
                && keyDocument.Contains(nameof(Tokens.ExpiresAt)));

            if (existing is null || !existing.TryGetValue("name", out var name))
            {
                // Nothing on ExpiresAt to amend. The conflict was about something else, so
                // leave the collection alone rather than inventing an index.
                return;
            }

            await _tokenCollection.Database.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "collMod", _tokenCollection.CollectionNamespace.CollectionName },
                { "index", new BsonDocument
                    {
                        { "name", name.AsString },
                        { "expireAfterSeconds", _ttlExpiry.TotalSeconds }
                    }
                }
            });
        }

        private static FilterDefinition<Tokens> FilterById(string idAsString) => Builders<Tokens>.Filter.Eq(t => t.Id, idAsString);

        /// <summary>
        /// A struct can always be default-initialised, so the validating constructor is not a
        /// guarantee. Rejecting it here stops an uninitialised identifier from reading or
        /// writing a document keyed on the empty string, which every default instance shares.
        /// </summary>
        private static string RequireIdentifier(TokenIdentifier id)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("The token identifier is uninitialised. Construct it with a value instead of using default(TokenIdentifier).", nameof(id));
            }

            return id.ToString();
        }

        public override async Task Consume(TokenIdentifier id)
        {
            var idAsString = RequireIdentifier(id);
            await EnsureTtlIndex();
            await _tokenCollection.DeleteOneAsync(FilterById(idAsString));
        }

        public override async Task<bool> ConsumeAndValidate(TokenIdentifier id, string token)
        {
            var idAsString = RequireIdentifier(id);

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            await EnsureTtlIndex();

            var tokenInDb = await _tokenCollection.Find(FilterById(idAsString)).FirstOrDefaultAsync();

            if (tokenInDb is null || !tokenInDb.Token.Valid(salt: idAsString, token, tokenInDb.ExpiresAt, _hashPepper))
            {
                // The stored token stays put. A wrong guess must not discard a token the
                // legitimate holder has not had a chance to use.
                return false;
            }

            // Claim it atomically, matching the hash just verified. If a concurrent caller got
            // there first the delete matches nothing and this call reports failure, so a token
            // can still only ever be consumed once. Matching on the hash also leaves a token
            // issued by a Generate that raced in between untouched.
            var claimed = await _tokenCollection.FindOneAndDeleteAsync(Builders<Tokens>.Filter.And(
                Builders<Tokens>.Filter.Eq(t => t.Id, idAsString),
                Builders<Tokens>.Filter.Eq(t => t.Token.OneTimeTokenHash, tokenInDb.Token.OneTimeTokenHash)));

            return claimed is not null;
        }

        public override async Task<string> Generate(string logId, TokenIdentifier id, int validityInSeconds, int numberOfDigits = 0)
        {
            var idAsString = RequireIdentifier(id);
            ArgumentOutOfRangeException.ThrowIfNegative(numberOfDigits);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(validityInSeconds);

            await EnsureTtlIndex();

            // No need to look for and delete an existing token first: the upsert below
            // replaces it in a single round trip, and deleting it beforehand left a window
            // in which a concurrent Validate saw no token at all.
            var oneTimeToken = (numberOfDigits > 0) ? Utils.GetRandomNumber(numberOfDigits) : Guid.NewGuid().ToString().ToLowerInvariant();

            var filter = FilterById(idAsString);
            var options = new ReplaceOptions { IsUpsert = true };
            var tokenValue = new TokenValue(salt: idAsString, oneTimeToken, _hashPepper);
            var expiresAt = DateTime.UtcNow.AddSeconds(validityInSeconds);
            await _tokenCollection.ReplaceOneAsync(filter, new Tokens() { LogId = logId, Id = idAsString, Token = tokenValue, ExpiresAt = expiresAt }, options);
            return oneTimeToken;
        }

        public override async Task<bool> Validate(TokenIdentifier id, string token)
        {
            var idAsString = RequireIdentifier(id);

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            await EnsureTtlIndex();

			var tokenInDb = await _tokenCollection.Find(FilterById(idAsString)).FirstOrDefaultAsync();

			if (tokenInDb is null)
			{
				return false;
			}

			return tokenInDb.Token.Valid(salt: idAsString, token, tokenInDb.ExpiresAt, _hashPepper);
        }
    }
}
