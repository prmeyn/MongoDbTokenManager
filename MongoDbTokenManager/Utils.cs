using System.Security.Cryptography;

namespace MongoDbTokenManager
{
	public static class Utils
	{
		public static string GetRandomNumber(int numberOfDigits)
		{
			// RandomNumberGenerator.GetInt32 rejection-samples, so each digit is uniform.
			// Taking bytes modulo 10 would favour 0-5, which occur in 26 of the 256 byte
			// values against 25 for 6-9.
			return string.Join(string.Empty, Enumerable.Range(0, numberOfDigits).Select(_ => RandomNumberGenerator.GetInt32(0, 10)));
		}
	}
}
