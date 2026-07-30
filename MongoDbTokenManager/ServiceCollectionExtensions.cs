using Microsoft.Extensions.DependencyInjection;
using MongoDbTokenManager.Database;

namespace MongoDbTokenManager
{
	public static class ServiceCollectionExtensions
	{
		public static void AddMongoDbTokenServices(this IServiceCollection services)
		{
			services.AddSingleton<MongoDbTokenService>();
			// Resolving the base type is what makes AbstractTokenService substitutable. It
			// forwards to the same singleton so both registrations share one instance.
			services.AddSingleton<AbstractTokenService>(sp => sp.GetRequiredService<MongoDbTokenService>());
		}
	}

	/// <summary>
	/// Misspelled original, kept so callers that referenced the class by name still compile.
	/// Renaming it outright would be a breaking change.
	/// </summary>
	[Obsolete("Use ServiceCollectionExtensions instead. This misspelled alias will be removed in the next major version.")]
	public static class SeviceCollectionExtensions
	{
		public static void AddMongoDbTokenServices(this IServiceCollection services) => ServiceCollectionExtensions.AddMongoDbTokenServices(services);
	}
}
