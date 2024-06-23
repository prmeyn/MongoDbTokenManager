using Microsoft.Extensions.DependencyInjection;
using MongoDbTokenManager.Database;

namespace MongoDbTokenManager
{
	public static class SeviceCollectionExtensions
	{
		public static void AddMongoDbTokenServices(this IServiceCollection services)
		{
			services.AddSingleton<MongoDbTokenService>();
		}
	}
}
