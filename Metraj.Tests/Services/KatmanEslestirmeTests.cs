using FluentAssertions;
using Metraj.Models;
using Xunit;

namespace Metraj.Tests.Services
{
    public class KatmanEslestirmeTests
    {
        [Fact]
        public void VarsayilanAyarlar_BosDegildir()
        {
            var ayarlar = KatmanEslestirmeAyarlari.VarsayilanOlustur();
            ayarlar.Should().NotBeNull();
            ayarlar.Eslestirmeler.Should().NotBeEmpty();
            ayarlar.Eslestirmeler.Count.Should().BeGreaterThan(10);
        }

        [Fact]
        public void VarsayilanAyarlar_TemelMalzemeleriIcerir()
        {
            var ayarlar = KatmanEslestirmeAyarlari.VarsayilanOlustur();
            ayarlar.Eslestirmeler.Should().Contain(e => e.MalzemeAdi == "A\u015F\u0131nma");
            ayarlar.Eslestirmeler.Should().Contain(e => e.MalzemeAdi == "Binder");
            ayarlar.Eslestirmeler.Should().Contain(e => e.MalzemeAdi == "Kaz\u0131");
            ayarlar.Eslestirmeler.Should().Contain(e => e.MalzemeAdi == "Dolgu");
        }

        [Fact]
        public void VarsayilanAyarlar_KategorileriDogru()
        {
            var ayarlar = KatmanEslestirmeAyarlari.VarsayilanOlustur();
            ayarlar.Eslestirmeler.Should().Contain(e =>
                e.MalzemeAdi == "A\u015F\u0131nma" && e.Kategori == MalzemeKategorisi.Ustyapi);
            ayarlar.Eslestirmeler.Should().Contain(e =>
                e.MalzemeAdi == "Plentmiks Temel" && e.Kategori == MalzemeKategorisi.Alttemel);
            ayarlar.Eslestirmeler.Should().Contain(e =>
                e.MalzemeAdi == "Kaz\u0131" && e.Kategori == MalzemeKategorisi.ToprakIsleri);
            ayarlar.Eslestirmeler.Should().Contain(e =>
                e.MalzemeAdi == "Banket" && e.Kategori == MalzemeKategorisi.Ozel);
        }

        [Fact]
        public void VarsayilanAyarlar_TumKurallarAktif()
        {
            var ayarlar = KatmanEslestirmeAyarlari.VarsayilanOlustur();
            ayarlar.Eslestirmeler.Should().OnlyContain(e => e.Aktif == true);
        }

        [Fact]
        public void KatmanEslestirme_VarsayilanAktifTrue()
        {
            var eslestirme = new KatmanEslestirme
            {
                LayerPattern = "TEST*",
                MalzemeAdi = "Test",
                Kategori = MalzemeKategorisi.Ozel
            };
            eslestirme.Aktif.Should().BeTrue();
        }

        [Fact]
        public void KatmanAlanBilgisi_PropertyAtamasiDogru()
        {
            var bilgi = new KatmanAlanBilgisi
            {
                MalzemeAdi = "A\u015F\u0131nma",
                Kategori = MalzemeKategorisi.Ustyapi,
                Alan = 12.5,
                KaynakLayerAdi = "ASINMA_1"
            };

            bilgi.MalzemeAdi.Should().Be("A\u015F\u0131nma");
            bilgi.Kategori.Should().Be(MalzemeKategorisi.Ustyapi);
            bilgi.Alan.Should().Be(12.5);
            bilgi.KaynakLayerAdi.Should().Be("ASINMA_1");
        }

        [Fact]
        public void YolKesitVerisi_MalzemeAlaniGetir_BulunanMalzeme()
        {
            var kesit = new YolKesitVerisi
            {
                Istasyon = 20,
                KatmanAlanlari = new System.Collections.Generic.List<KatmanAlanBilgisi>
                {
                    new KatmanAlanBilgisi { MalzemeAdi = "A\u015F\u0131nma", Alan = 1.5 },
                    new KatmanAlanBilgisi { MalzemeAdi = "Kaz\u0131", Alan = 12.3 }
                }
            };

            kesit.MalzemeAlaniGetir("A\u015F\u0131nma").Should().Be(1.5);
            kesit.MalzemeAlaniGetir("Kaz\u0131").Should().Be(12.3);
        }

        [Fact]
        public void YolKesitVerisi_MalzemeAlaniGetir_BulunamayanMalzeme_SifirDoner()
        {
            var kesit = new YolKesitVerisi
            {
                Istasyon = 20,
                KatmanAlanlari = new System.Collections.Generic.List<KatmanAlanBilgisi>
                {
                    new KatmanAlanBilgisi { MalzemeAdi = "A\u015F\u0131nma", Alan = 1.5 }
                }
            };

            kesit.MalzemeAlaniGetir("Dolgu").Should().Be(0);
        }

        [Fact]
        public void YolKesitVerisi_MalzemeAlaniGetir_CaseInsensitive()
        {
            var kesit = new YolKesitVerisi
            {
                Istasyon = 20,
                KatmanAlanlari = new System.Collections.Generic.List<KatmanAlanBilgisi>
                {
                    new KatmanAlanBilgisi { MalzemeAdi = "A\u015F\u0131nma", Alan = 1.5 }
                }
            };

            kesit.MalzemeAlaniGetir("a\u015F\u0131nma").Should().Be(1.5);
        }
    }
}
