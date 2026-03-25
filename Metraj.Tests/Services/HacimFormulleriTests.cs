using FluentAssertions;
using Metraj.Models;
using Metraj.Services;
using Xunit;

namespace Metraj.Tests.Services
{
    public class HacimFormulleriTests
    {
        [Fact]
        public void OrtalamaAlan_BasitHesap()
        {
            // V = (A1 + A2) / 2 * L = (10 + 20) / 2 * 30 = 450
            double sonuc = HacimFormulleri.Hesapla(10, 20, 30, HacimMetodu.OrtalamaAlan);
            sonuc.Should().BeApproximately(450.0, 0.001);
        }

        [Fact]
        public void Prismoidal_BasitHesap()
        {
            // Am = (10 + 20) / 2 = 15
            // V = L/6 * (A1 + 4*Am + A2) = 30/6 * (10 + 60 + 20) = 5 * 90 = 450
            double sonuc = HacimFormulleri.Hesapla(10, 20, 30, HacimMetodu.Prismoidal);
            sonuc.Should().BeApproximately(450.0, 0.001);
        }

        [Fact]
        public void OrtalamaAlan_EsitAlanlar()
        {
            // V = (15 + 15) / 2 * 20 = 300
            double sonuc = HacimFormulleri.Hesapla(15, 15, 20, HacimMetodu.OrtalamaAlan);
            sonuc.Should().BeApproximately(300.0, 0.001);
        }

        [Fact]
        public void SifirMesafe_SifirDoner()
        {
            double sonuc = HacimFormulleri.Hesapla(10, 20, 0, HacimMetodu.OrtalamaAlan);
            sonuc.Should().Be(0);
        }

        [Fact]
        public void NegatifMesafe_SifirDoner()
        {
            double sonuc = HacimFormulleri.Hesapla(10, 20, -5, HacimMetodu.OrtalamaAlan);
            sonuc.Should().Be(0);
        }

        [Fact]
        public void SifirAlanlar_SifirDoner()
        {
            double sonuc = HacimFormulleri.Hesapla(0, 0, 20, HacimMetodu.OrtalamaAlan);
            sonuc.Should().Be(0);
        }

        [Fact]
        public void BirAlanSifir_DogurHesaplar()
        {
            // V = (0 + 10) / 2 * 20 = 100
            double sonuc = HacimFormulleri.Hesapla(0, 10, 20, HacimMetodu.OrtalamaAlan);
            sonuc.Should().BeApproximately(100.0, 0.001);
        }

        [Theory]
        [InlineData(5.0, 8.0, 20.0, HacimMetodu.OrtalamaAlan, 130.0)]   // (5+8)/2*20 = 130
        [InlineData(100.0, 100.0, 10.0, HacimMetodu.OrtalamaAlan, 1000.0)]
        public void CesitliDegerler_OrtalamaAlan(double a1, double a2, double m, HacimMetodu metot, double beklenen)
        {
            double sonuc = HacimFormulleri.Hesapla(a1, a2, m, metot);
            sonuc.Should().BeApproximately(beklenen, 0.01);
        }
    }
}
