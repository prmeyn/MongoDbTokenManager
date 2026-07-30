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
}
