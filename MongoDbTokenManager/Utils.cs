using System.Security.Cryptography;

namespace MongoDbTokenManager
{
	public static class Utils
	{
		private static readonly RandomNumberGenerator RandomNumberGenerator = RandomNumberGenerator.Create();

		public static string GetRandomNumber(int numberOfDigits)
		{
			var bytes = new byte[numberOfDigits];
			RandomNumberGenerator.GetBytes(bytes);
			return string.Join(string.Empty, bytes.Select(b => b % 10));
		}
	}
}
