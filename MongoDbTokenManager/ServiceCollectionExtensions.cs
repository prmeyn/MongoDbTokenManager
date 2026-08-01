using Microsoft.Extensions.DependencyInjection;
using MongoDbService;
using MongoDbTokenManager.Database;

namespace MongoDbTokenManager
{
	public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Registers the token service.
		/// </summary>
		/// <param name="services">The service collection to add the registrations to.</param>
		/// <param name="cleanupAfterExpiry">
		/// How long an expired token is kept before the TTL index removes it. Defaults to 24 hours.
		/// <see cref="TimeSpan.Zero"/> deletes tokens as soon as they expire.
		/// </param>
		/// <param name="hashPepper">
		/// Secret that keys the stored digest (HMAC-SHA512) so a leaked collection cannot be searched
		/// offline for a short numeric token. Read it from a secret store, never from a checked-in
		/// appsettings file. Tokens issued under a different pepper - or none - stop validating, so
		/// introduce it when nothing is in flight.
		/// </param>
		public static void AddMongoDbTokenServices(
			this IServiceCollection services,
			TimeSpan? cleanupAfterExpiry = null,
			string? hashPepper = null)
		{
			// Built through a factory rather than AddSingleton<MongoDbTokenService>() so the options
			// above are actually applied: the container fills a constructor's optional parameters
			// from their defaults, so type-based registration could only ever produce an unpeppered
			// service on the default TTL.
			services.AddSingleton(serviceProvider => new MongoDbTokenService(
				serviceProvider.GetRequiredService<MongoService>(),
				cleanupAfterExpiry,
				hashPepper));

			// Resolving the base type is what makes AbstractTokenService substitutable. It
			// forwards to the same singleton so both registrations share one instance.
			services.AddSingleton<AbstractTokenService>(sp => sp.GetRequiredService<MongoDbTokenService>());
		}
	}
}
