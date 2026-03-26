using System.Linq;
using FluentAssertions;
using Metraj.Models;
using Xunit;

namespace Metraj.Tests.Services
{
    public class MalzemeHatchAyarTests
    {
        [Fact]
        public void VarsayilanOlustur_EnAz10Malzeme()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            ayarlar.Ayarlar.Count.Should().BeGreaterThanOrEqualTo(10);
        }

        [Fact]
        public void VarsayilanOlustur_TumMalzemeAdlariBenzersiz()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            var adlar = ayarlar.Ayarlar.Select(a => a.MalzemeAdi).ToList();
            adlar.Distinct().Count().Should().Be(adlar.Count);
        }

        [Fact]
        public void VarsayilanOlustur_TumRenklerGecerli()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            ayarlar.Ayarlar.Should().OnlyContain(a => a.RenkIndex >= 0 && a.RenkIndex <= 256);
        }

        [Fact]
        public void VarsayilanOlustur_TumSeffaflikGecerli()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            ayarlar.Ayarlar.Should().OnlyContain(a => a.Seffaflik >= 0 && a.Seffaflik <= 1);
        }

        [Fact]
        public void VarsayilanOlustur_TemelMalzemeleriIcerir()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            ayarlar.Ayarlar.Should().Contain(a => a.MalzemeAdi == "Yarma");
            ayarlar.Ayarlar.Should().Contain(a => a.MalzemeAdi == "Dolgu");
            ayarlar.Ayarlar.Should().Contain(a => a.MalzemeAdi == "Binder");
            ayarlar.Ayarlar.Should().Contain(a => a.MalzemeAdi == "Plentmiks");
        }

        [Fact]
        public void AyarGetir_VarOlanMalzeme_Doner()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            var ayar = ayarlar.AyarGetir("Yarma");
            ayar.Should().NotBeNull();
            ayar.RenkIndex.Should().Be(3);
            ayar.KisaKod.Should().Be("Y");
        }

        [Fact]
        public void AyarGetir_CaseInsensitive()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            var ayar = ayarlar.AyarGetir("yarma");
            ayar.Should().NotBeNull();
            ayar.MalzemeAdi.Should().Be("Yarma");
        }

        [Fact]
        public void AyarGetir_OlmayanMalzeme_VarsayilanDoner()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            var ayar = ayarlar.AyarGetir("BilinmeyenMalzeme");
            ayar.Should().NotBeNull();
            ayar.MalzemeAdi.Should().Be("BilinmeyenMalzeme");
            ayar.RenkIndex.Should().Be(7);
        }

        [Fact]
        public void AyarGetir_Null_NullDoner()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            ayarlar.AyarGetir(null).Should().BeNull();
        }

        [Fact]
        public void YolKolonu_BosIstasyonListesi()
        {
            var kolon = new YolKolonu { KolonHarfi = "A", Aciklama = "Test" };
            kolon.Istasyonlar.Should().NotBeNull();
            kolon.Istasyonlar.Should().BeEmpty();
        }

        [Fact]
        public void TumKisaKodlarBenzersiz()
        {
            var ayarlar = MalzemeHatchAyarlari.VarsayilanOlustur();
            var kodlar = ayarlar.Ayarlar.Select(a => a.KisaKod).ToList();
            kodlar.Distinct().Count().Should().Be(kodlar.Count);
        }
    }
}
