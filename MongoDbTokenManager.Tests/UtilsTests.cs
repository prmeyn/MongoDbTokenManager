using Xunit;

namespace MongoDbTokenManager.Tests;

public class UtilsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(10)]
    public void GetRandomNumber_ReturnsExactlyThatManyDigits(int numberOfDigits)
    {
        var result = Utils.GetRandomNumber(numberOfDigits);

        Assert.Equal(numberOfDigits, result.Length);
        Assert.All(result, c => Assert.True(char.IsAsciiDigit(c), $"'{c}' is not a digit"));
    }

    [Fact]
    public void GetRandomNumber_ZeroDigitsReturnsEmpty()
    {
        Assert.Equal(string.Empty, Utils.GetRandomNumber(0));
    }

    [Fact]
    public void GetRandomNumber_PreservesLeadingZeros()
    {
        // Generated as a string rather than an int precisely so a code like "004321" keeps
        // its width. Sample enough to be near-certain of seeing at least one leading zero.
        var sawLeadingZero = Enumerable.Range(0, 500).Any(_ => Utils.GetRandomNumber(6).StartsWith('0'));

        Assert.True(sawLeadingZero, "expected at least one 6-digit code starting with 0");
    }

    [Fact]
    public void GetRandomNumber_DigitsAreNotBiasedTowardsLowValues()
    {
        // Taking random bytes modulo 10 favours 0-5, which arise from 26 of the 256 byte
        // values against 25 for 6-9 - roughly a 4% excess. Over 60k digits that skew is far
        // larger than sampling noise, so compare the two halves of the range.
        var counts = new int[10];
        for (var i = 0; i < 6000; i++)
        {
            foreach (var c in Utils.GetRandomNumber(10))
            {
                counts[c - '0']++;
            }
        }

        var low = counts[0] + counts[1] + counts[2] + counts[3] + counts[4] + counts[5];
        var high = counts[6] + counts[7] + counts[8] + counts[9];
        var expectedRatio = 6d / 4d;
        var actualRatio = (double)low / high;

        Assert.All(counts, count => Assert.True(count > 0, "every digit should appear"));
        Assert.True(Math.Abs(actualRatio - expectedRatio) < 0.05, $"digit distribution looks biased: low/high was {actualRatio:F3}, expected about {expectedRatio:F3}");
    }
}
