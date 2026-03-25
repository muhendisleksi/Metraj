using System;
using System.Collections.Generic;
using System.Linq;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public class YolKubajService : IYolKubajService
    {
        public YolKubajSonucu KubajHesapla(List<YolKesitVerisi> kesitler, HacimMetodu metot)
        {
            var sonuc = new YolKubajSonucu { Metot = metot };

            if (kesitler == null || kesitler.Count < 2)
                return sonuc;

            var sirali = kesitler.OrderBy(k => k.Istasyon).ToList();

            // Benzersiz malzeme adlarını topla
            var malzemeler = sirali
                .SelectMany(k => k.KatmanAlanlari)
                .Select(k => k.MalzemeAdi)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var malzeme in malzemeler)
            {
                var ozet = new MalzemeHacimOzeti
                {
                    MalzemeAdi = malzeme,
                    Kategori = sirali.SelectMany(k => k.KatmanAlanlari)
                        .FirstOrDefault(k => k.MalzemeAdi.Equals(malzeme, StringComparison.OrdinalIgnoreCase))
                        ?.Kategori ?? MalzemeKategorisi.Ozel
                };

                for (int i = 0; i < sirali.Count - 1; i++)
                {
                    var k1 = sirali[i];
                    var k2 = sirali[i + 1];
                    double alan1 = k1.MalzemeAlaniGetir(malzeme);
                    double alan2 = k2.MalzemeAlaniGetir(malzeme);
                    double mesafe = k2.Istasyon - k1.Istasyon;

                    if (mesafe <= 0) continue;

                    double hacim = HacimFormulleri.Hesapla(alan1, alan2, mesafe, metot);

                    // Tatbik mesafesi hesapla
                    double kaziAlani = (k1.ToplamKaziAlani + k2.ToplamKaziAlani) / 2.0;
                    double dolguAlani = (k1.ToplamDolguAlani + k2.ToplamDolguAlani) / 2.0;
                    bool kaziIcin = ozet.Kategori == MalzemeKategorisi.ToprakIsleri &&
                                    malzeme.Equals("Kaz\u0131", StringComparison.OrdinalIgnoreCase);
                    double tatbik = TatbikMesafesiHesapla(kaziAlani, dolguAlani, mesafe, kaziIcin);

                    ozet.Segmentler.Add(new KatmanHacimSegmenti
                    {
                        MalzemeAdi = malzeme,
                        Istasyon1 = k1.Istasyon,
                        Istasyon2 = k2.Istasyon,
                        Alan1 = alan1,
                        Alan2 = alan2,
                        Hacim = hacim,
                        Mesafe = mesafe,
                        TatbikMesafesi = tatbik
                    });
                }

                ozet.ToplamHacim = ozet.Segmentler.Sum(s => s.Hacim);
                sonuc.MalzemeOzetleri.Add(ozet);
            }

            // Toplam kazı/dolgu hacmi
            sonuc.ToplamKaziHacmi = sonuc.MalzemeOzetleri
                .Where(m => m.Kategori == MalzemeKategorisi.ToprakIsleri &&
                            m.MalzemeAdi.Equals("Kaz\u0131", StringComparison.OrdinalIgnoreCase))
                .Sum(m => m.ToplamHacim);

            sonuc.ToplamDolguHacmi = sonuc.MalzemeOzetleri
                .Where(m => m.Kategori == MalzemeKategorisi.ToprakIsleri &&
                            m.MalzemeAdi.Equals("Dolgu", StringComparison.OrdinalIgnoreCase))
                .Sum(m => m.ToplamHacim);

            sonuc.NetHacim = sonuc.ToplamKaziHacmi - sonuc.ToplamDolguHacmi;
            sonuc.BrucknerVerisi = BrucknerHesapla(kesitler, metot);

            LoggingService.Info("Yol k\u00FCbaj hesapland\u0131: {Metot}, {MalzemeCount} malzeme, kazı {Kazi:F2} m\u00B3, dolgu {Dolgu:F2} m\u00B3",
                metot, malzemeler.Count, sonuc.ToplamKaziHacmi, sonuc.ToplamDolguHacmi);

            return sonuc;
        }

        public double SegmentHacimHesapla(double alan1, double alan2, double mesafe, HacimMetodu metot)
        {
            return HacimFormulleri.Hesapla(alan1, alan2, mesafe, metot);
        }

        public double TatbikMesafesiHesapla(double kaziAlani, double dolguAlani, double mesafe, bool kaziIcin)
        {
            double toplam = kaziAlani + dolguAlani;
            if (toplam <= 0) return 0;

            if (kaziIcin)
                return (kaziAlani / toplam) * mesafe;
            else
                return (dolguAlani / toplam) * mesafe;
        }

        public List<BrucknerNoktasi> BrucknerHesapla(List<YolKesitVerisi> kesitler, HacimMetodu metot)
        {
            var bruckner = new List<BrucknerNoktasi>();
            if (kesitler == null || kesitler.Count < 2)
                return bruckner;

            var sirali = kesitler.OrderBy(k => k.Istasyon).ToList();
            double kumulatif = 0;

            bruckner.Add(new BrucknerNoktasi
            {
                Istasyon = sirali[0].Istasyon,
                KumulatifHacim = 0
            });

            for (int i = 0; i < sirali.Count - 1; i++)
            {
                var k1 = sirali[i];
                var k2 = sirali[i + 1];
                double mesafe = k2.Istasyon - k1.Istasyon;
                if (mesafe <= 0) continue;

                double kaziHacim = HacimFormulleri.Hesapla(k1.ToplamKaziAlani, k2.ToplamKaziAlani, mesafe, metot);
                double dolguHacim = HacimFormulleri.Hesapla(k1.ToplamDolguAlani, k2.ToplamDolguAlani, mesafe, metot);

                kumulatif += kaziHacim - dolguHacim;

                bruckner.Add(new BrucknerNoktasi
                {
                    Istasyon = k2.Istasyon,
                    KumulatifHacim = kumulatif
                });
            }

            return bruckner;
        }
    }
}
