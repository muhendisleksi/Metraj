using FluentAssertions;
using Metraj.Services;
using Xunit;

namespace Metraj.Tests.Services
{
    public class NumberParserHelperTests
    {
        [Theory]
        [InlineData("123", 123.0)]
        [InlineData("123.45", 123.45)]
        [InlineData("123,45", 123.45)]
        [InlineData("1.234,56", 1234.56)]
        [InlineData("1,234.56", 1234.56)]
        [InlineData("-12.5", -12.5)]
        public void TryParse_ValidNumbers_ReturnsTrue(string input, double expected)
        {
            NumberParserHelper.TryParse(input, out double result).Should().BeTrue();
            result.Should().BeApproximately(expected, 0.001);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("abc")]
        public void TryParse_InvalidNumbers_ReturnsFalse(string input)
        {
            NumberParserHelper.TryParse(input, out _).Should().BeFalse();
        }

        [Theory]
        [InlineData("A=125,30", "125,30")]
        [InlineData("45.6 m\u00B2", "45.6")]
        [InlineData("L: 12.5m", "12.5")]
        [InlineData("123", "123")]
        public void ExtractNumber_ExtractsCorrectly(string input, string expected)
        {
            NumberParserHelper.ExtractNumber(input).Should().Be(expected);
        }

        [Theory]
        [InlineData("A=125", "A=", "125")]
        [InlineData("Total: 50", "Total: ", "50")]
        public void StripPrefix_StripsCorrectly(string input, string prefix, string expected)
        {
            NumberParserHelper.StripPrefix(input, prefix).Should().Be(expected);
        }
    }
}
