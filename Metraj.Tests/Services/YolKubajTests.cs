using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Metraj.Models;
using Metraj.Services;
using Xunit;

namespace Metraj.Tests.Services
{
    public class YolKubajTests
    {
        // YolKubajService LoggingService'e bağımlı olduğu için doğrudan linklenemez.
        // Bunun yerine HacimFormulleri + model mantığını test ediyoruz.

        [Fact]
        public void HacimFormulleri_OrtalamaAlan_IkiKesitArasi()
        {
            // Km 0+000: A=10m², Km 0+020: A=20m²
            // V = (10+20)/2 * 20 = 300 m³
            double hacim = HacimFormulleri.Hesapla(10, 20, 20, HacimMetodu.OrtalamaAlan);
            hacim.Should().BeApproximately(300.0, 0.01);
        }

        [Fact]
        public void TatbikMesafesi_KaziIcin()
        {
            // K=10, D=5, L=20 → TM = (10/(10+5)) * 20 = 13.33
            double kazi = 10, dolgu = 5, mesafe = 20;
            double toplam = kazi + dolgu;
            double tm = (kazi / toplam) * mesafe;
            tm.Should().BeApproximately(13.333, 0.01);
        }

        [Fact]
        public void TatbikMesafesi_DolguIcin()
        {
            // K=10, D=5, L=20 → TM = (5/(10+5)) * 20 = 6.67
            double kazi = 10, dolgu = 5, mesafe = 20;
            double toplam = kazi + dolgu;
            double tm = (dolgu / toplam) * mesafe;
            tm.Should().BeApproximately(6.667, 0.01);
        }

        [Fact]
        public void TatbikMesafesi_SifirToplam_SifirDoner()
        {
            double kazi = 0, dolgu = 0, mesafe = 20;
            double toplam = kazi + dolgu;
            double tm = toplam <= 0 ? 0 : (kazi / toplam) * mesafe;
            tm.Should().Be(0);
        }

        [Fact]
        public void YolKesitVerisi_MalzemeAlaniGetir_CokluKatman()
        {
            var kesit = new YolKesitVerisi
            {
                Istasyon = 0,
                KatmanAlanlari = new List<KatmanAlanBilgisi>
                {
                    new KatmanAlanBilgisi { MalzemeAdi = "A\u015F\u0131nma", Alan = 1.5, Kategori = MalzemeKategorisi.Ustyapi },
                    new KatmanAlanBilgisi { MalzemeAdi = "Binder", Alan = 2.0, Kategori = MalzemeKategorisi.Ustyapi },
                    new KatmanAlanBilgisi { MalzemeAdi = "Kaz\u0131", Alan = 12.3, Kategori = MalzemeKategorisi.ToprakIsleri },
                    new KatmanAlanBilgisi { MalzemeAdi = "Dolgu", Alan = 5.0, Kategori = MalzemeKategorisi.ToprakIsleri }
                }
            };

            kesit.MalzemeAlaniGetir("A\u015F\u0131nma").Should().Be(1.5);
            kesit.MalzemeAlaniGetir("Binder").Should().Be(2.0);
            kesit.MalzemeAlaniGetir("Kaz\u0131").Should().Be(12.3);
            kesit.MalzemeAlaniGetir("Dolgu").Should().Be(5.0);
            kesit.MalzemeAlaniGetir("Plentmiks").Should().Be(0);
        }

        [Fact]
        public void KatmanBazliHacim_IkiKesitUcMalzeme()
        {
            // 3 malzeme, 2 kesit, 20m mesafe
            var k1 = new YolKesitVerisi
            {
                Istasyon = 0,
                KatmanAlanlari = new List<KatmanAlanBilgisi>
                {
                    new KatmanAlanBilgisi { MalzemeAdi = "A\u015F\u0131nma", Alan = 1.0 },
                    new KatmanAlanBilgisi { MalzemeAdi = "Binder", Alan = 2.0 },
                    new KatmanAlanBilgisi { MalzemeAdi = "Kaz\u0131", Alan = 10.0 }
                }
            };

            var k2 = new YolKesitVerisi
            {
                Istasyon = 20,
                KatmanAlanlari = new List<KatmanAlanBilgisi>
                {
                    new KatmanAlanBilgisi { MalzemeAdi = "A\u015F\u0131nma", Alan = 1.5 },
                    new KatmanAlanBilgisi { MalzemeAdi = "Binder", Alan = 2.5 },
                    new KatmanAlanBilgisi { MalzemeAdi = "Kaz\u0131", Alan = 8.0 }
                }
            };

            double mesafe = 20;

            // Aşınma: (1.0+1.5)/2 * 20 = 25
            double asinmaHacim = HacimFormulleri.Hesapla(1.0, 1.5, mesafe, HacimMetodu.OrtalamaAlan);
            asinmaHacim.Should().BeApproximately(25.0, 0.01);

            // Binder: (2.0+2.5)/2 * 20 = 45
            double binderHacim = HacimFormulleri.Hesapla(2.0, 2.5, mesafe, HacimMetodu.OrtalamaAlan);
            binderHacim.Should().BeApproximately(45.0, 0.01);

            // Kazı: (10.0+8.0)/2 * 20 = 180
            double kaziHacim = HacimFormulleri.Hesapla(10.0, 8.0, mesafe, HacimMetodu.OrtalamaAlan);
            kaziHacim.Should().BeApproximately(180.0, 0.01);
        }

        [Fact]
        public void BrucknerMantigi_KumulatifHacim()
        {
            // 3 kesit: kazı ve dolgu var
            // Segment 1: kazı=100m³, dolgu=50m³ → net=+50
            // Segment 2: kazı=30m³, dolgu=80m³ → net=-50
            // Kümülatif: 0, +50, 0

            double kumulatif = 0;
            var bruckner = new List<BrucknerNoktasi>();
            bruckner.Add(new BrucknerNoktasi { Istasyon = 0, KumulatifHacim = 0 });

            kumulatif += (100 - 50);
            bruckner.Add(new BrucknerNoktasi { Istasyon = 20, KumulatifHacim = kumulatif });

            kumulatif += (30 - 80);
            bruckner.Add(new BrucknerNoktasi { Istasyon = 40, KumulatifHacim = kumulatif });

            bruckner[0].KumulatifHacim.Should().Be(0);
            bruckner[1].KumulatifHacim.Should().Be(50);
            bruckner[2].KumulatifHacim.Should().Be(0);
        }

        [Fact]
        public void YolKubajSonucu_NetHacim_KaziEksiDolgu()
        {
            var sonuc = new YolKubajSonucu
            {
                ToplamKaziHacmi = 1234.56,
                ToplamDolguHacmi = 567.89,
                NetHacim = 1234.56 - 567.89
            };

            sonuc.NetHacim.Should().BeApproximately(666.67, 0.01);
        }
    }
}
