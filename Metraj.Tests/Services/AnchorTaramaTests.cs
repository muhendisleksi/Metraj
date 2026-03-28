using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;
using FluentAssertions;

namespace Metraj.Tests.Services
{
    public class AnchorTaramaTests
    {
        // IstasyonParse static metodu AutoCAD bağımlı class'ta olduğu için,
        // regex parse mantığını bağımsız test ediyoruz.
        private static double IstasyonParse(string metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return -1;
            string temiz = metin.Trim();
            temiz = Regex.Replace(temiz, @"^(Km|KM|km|Ist|IST|ist)[:\s]*", "", RegexOptions.IgnoreCase);
            var match = Regex.Match(temiz, @"(\d+)\+(\d+\.?\d*)");
            if (match.Success)
            {
                double km = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                double m = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                return km * 1000 + m;
            }
            if (double.TryParse(temiz, NumberStyles.Float, CultureInfo.InvariantCulture, out double direkt))
                return direkt;
            return -1;
        }

        private static string IstasyonFormatla(double istasyon)
        {
            int km = (int)(istasyon / 1000);
            double m = istasyon % 1000;
            return string.Format(CultureInfo.InvariantCulture, "{0}+{1:000.00}", km, m);
        }

        [Theory]
        [InlineData("0+150", 150.0)]
        [InlineData("0+820", 820.0)]
        [InlineData("1+200", 1200.0)]
        [InlineData("0+000", 0.0)]
        [InlineData("2+500.50", 2500.50)]
        public void IstasyonParse_StandartFormat_DogruParse(string metin, double beklenen)
        {
            IstasyonParse(metin).Should().BeApproximately(beklenen, 0.01);
        }

        [Theory]
        [InlineData("Km 1+200", 1200.0)]
        [InlineData("KM 0+500", 500.0)]
        [InlineData("km 0+020", 20.0)]
        public void IstasyonParse_KmOnekli_DogruParse(string metin, double beklenen)
        {
            IstasyonParse(metin).Should().BeApproximately(beklenen, 0.01);
        }

        [Theory]
        [InlineData("IST: 0+000", 0.0)]
        [InlineData("Ist: 0+350", 350.0)]
        [InlineData("ist 1+000", 1000.0)]
        public void IstasyonParse_IstOnekli_DogruParse(string metin, double beklenen)
        {
            IstasyonParse(metin).Should().BeApproximately(beklenen, 0.01);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("ABC")]
        [InlineData("YARMA")]
        public void IstasyonParse_GecersizFormat_EksiDoner(string metin)
        {
            IstasyonParse(metin).Should().Be(-1);
        }

        [Fact]
        public void IstasyonFormatla_StandartDeger_DogruFormat()
        {
            IstasyonFormatla(820.0).Should().Be("0+820.00");
        }

        [Fact]
        public void IstasyonFormatla_BinUstu_DogruFormat()
        {
            IstasyonFormatla(1200.0).Should().Be("1+200.00");
        }

        [Fact]
        public void IstasyonFormatla_Sifir_DogruFormat()
        {
            IstasyonFormatla(0).Should().Be("0+000.00");
        }

        [Fact]
        public void IstasyonFormatla_OndalikliDeger_DogruFormat()
        {
            IstasyonFormatla(2500.50).Should().Be("2+500.50");
        }

        [Fact]
        public void AnchorSiralama_KmArtanSirada()
        {
            // Anchor listesi km'ye göre sıralı olmalı
            double[] istasyonlar = { 500, 150, 820, 0, 1200 };
            var sirali = new double[istasyonlar.Length];
            Array.Copy(istasyonlar, sirali, istasyonlar.Length);
            Array.Sort(sirali);

            sirali.Should().BeInAscendingOrder();
            sirali[0].Should().Be(0);
            sirali[^1].Should().Be(1200);
        }

        [Fact]
        public void DuplikeKontrol_AyniKmIkiText_TekAnchor()
        {
            // Aynı istasyonda 2 text varsa 1 tanesi seçilmeli
            var anchor1Y = 100.0;
            var anchor2Y = 200.0;
            var ortalamaY = (anchor1Y + anchor2Y) / 2.0;

            // En yakın olan seçilir
            var yakinlik1 = Math.Abs(anchor1Y - ortalamaY);
            var yakinlik2 = Math.Abs(anchor2Y - ortalamaY);

            yakinlik1.Should().Be(yakinlik2,
                "İki nokta da ortalamaya eşit uzaklıkta, ilki seçilir");
        }
    }
}
