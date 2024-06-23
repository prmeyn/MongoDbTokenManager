﻿using MongoDB.Driver;
using MongoDbService;
using MongoDbTokenManager.Database.DTOs;

namespace MongoDbTokenManager.Database
{
	public sealed class MongoDbTokenService : AbstractTokenService
    {
        private IMongoCollection<Tokens> _tokenCollection;

        public MongoDbTokenService(
			MongoService mongoService)
        {
            _tokenCollection = mongoService.Database.GetCollection<Tokens>(nameof(Tokens), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });
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
            if (tokenInDb != null && tokenInDb.Token.Valid(tokenInDb.Token.OneTimeToken))
            {
                oneTimeToken = tokenInDb.Token.OneTimeToken;
                await Consume(id);
            }
            else
            {
                oneTimeToken = (numberOfDigits > 0) ? Utils.GetRandomNumber(numberOfDigits) : Guid.NewGuid().ToString().ToLowerInvariant();
            }

            var idAsString = id.ToString();
            var filter = Builders<Tokens>.Filter.Eq(t => t.Id, idAsString);
            var options = new ReplaceOptions { IsUpsert = true };
            await _tokenCollection.ReplaceOneAsync(filter, new Tokens() { LogId = logId, Id = idAsString, Token = new TokenValue(oneTimeToken, validityInSeconds) }, options);
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

            if (tokenInDb.ValidationAttemptsTimeStamps == null || tokenInDb.ValidationAttemptsTimeStamps.Count() == 0)
            {
                tokenInDb.ValidationAttemptsTimeStamps = [DateTimeOffset.UtcNow];
            }
            else
            {
                tokenInDb.ValidationAttemptsTimeStamps.Add(DateTimeOffset.UtcNow);
			}

			
			var options = new ReplaceOptions { IsUpsert = true };
			_ = _tokenCollection.ReplaceOneAsync(filter, tokenInDb, options);

			return await Task.FromResult(tokenInDb?.Token?.Valid(token) ?? false);
        }
    }
}
