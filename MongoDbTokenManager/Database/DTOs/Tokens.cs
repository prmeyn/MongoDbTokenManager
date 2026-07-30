using MongoDB.Bson.Serialization.Attributes;

namespace MongoDbTokenManager.Database.DTOs
{
    public sealed class Tokens
    {
        [BsonId]
        public required string Id { get; set; }
        public required TokenValue Token { get; set; }
        public required string LogId { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime ExpiresAt { get; set; }
    }
}
