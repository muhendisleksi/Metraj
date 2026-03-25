using FluentAssertions;
using Xunit;

namespace Metraj.Tests.Services
{
    public class IstasyonParseTests
    {
        // IstasyonParse AutoCAD bağımlı serviste olduğu için
        // regex mantığını doğrudan test ediyoruz
        private static double IstasyonParse(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin))
                return -1;

            string temiz = metin.Trim();
            temiz = System.Text.RegularExpressions.Regex.Replace(
                temiz, @"^(Km|KM|km|Ist|IST|ist)[:\s]*", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var match = System.Text.RegularExpressions.Regex.Match(temiz, @"(\d+)\+(\d+\.?\d*)");
            if (match.Success)
            {
                double km = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                double m = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                return km * 1000 + m;
            }

            if (double.TryParse(temiz, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double direkt))
                return direkt;

            return -1;
        }

        [Theory]
        [InlineData("0+000", 0.0)]
        [InlineData("0+020", 20.0)]
        [InlineData("0+020.00", 20.0)]
        [InlineData("0+100", 100.0)]
        [InlineData("1+234.567", 1234.567)]
        [InlineData("2+500", 2500.0)]
        public void TemelFormatlar_DogruParse(string metin, double beklenen)
        {
            IstasyonParse(metin).Should().BeApproximately(beklenen, 0.001);
        }

        [Theory]
        [InlineData("Km 0+100", 100.0)]
        [InlineData("KM:0+050", 50.0)]
        [InlineData("km 1+200", 1200.0)]
        [InlineData("Ist: 0+200", 200.0)]
        [InlineData("IST 0+300", 300.0)]
        public void OnekliFormatlar_DogruParse(string metin, double beklenen)
        {
            IstasyonParse(metin).Should().BeApproximately(beklenen, 0.001);
        }

        [Theory]
        [InlineData("20", 20.0)]
        [InlineData("100.5", 100.5)]
        [InlineData("1234.567", 1234.567)]
        public void SadeceSayi_DogruParse(string metin, double beklenen)
        {
            IstasyonParse(metin).Should().BeApproximately(beklenen, 0.001);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("abc")]
        public void GecersizDegerler_NegatifDoner(string metin)
        {
            IstasyonParse(metin).Should().BeNegative();
        }

        [Fact]
        public void SifirIstasyon_SifirDoner()
        {
            IstasyonParse("0+000").Should().Be(0.0);
        }

        [Fact]
        public void BuyukIstasyon_DogruParse()
        {
            // 10+500 = 10500 m
            IstasyonParse("10+500").Should().BeApproximately(10500.0, 0.001);
        }
    }
}
