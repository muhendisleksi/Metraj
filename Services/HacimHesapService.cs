using System;
using System.Collections.Generic;
using System.Linq;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public class HacimHesapService : IHacimHesapService
    {
        public HacimHesapSonucu HesaplaEnkesittenHacim(List<EnKesitVerisi> enkesitler, HacimMetodu metot)
        {
            var sonuc = new HacimHesapSonucu { Metot = metot };

            if (enkesitler == null || enkesitler.Count < 2)
                return sonuc;

            // İstasyona göre sırala
            var sirali = enkesitler.OrderBy(e => e.Istasyon).ToList();

            double toplamHacim = 0;

            for (int i = 0; i < sirali.Count - 1; i++)
            {
                var e1 = sirali[i];
                var e2 = sirali[i + 1];
                double mesafe = e2.Istasyon - e1.Istasyon;
                double hacim;

                if (mesafe <= 0) continue;

                hacim = HacimFormulleri.Hesapla(e1.ToplamAlan, e2.ToplamAlan, mesafe, metot);

                sonuc.Segmentler.Add(new HacimSegmenti
                {
                    Istasyon1 = e1.Istasyon,
                    Istasyon2 = e2.Istasyon,
                    Alan1 = e1.ToplamAlan,
                    Alan2 = e2.ToplamAlan,
                    Hacim = hacim,
                    Mesafe = mesafe
                });

                toplamHacim += hacim;
            }

            sonuc.ToplamHacim = toplamHacim;
            sonuc.BrucknerVerisi = BrucknerHesapla(sonuc);

            LoggingService.Info("Hacim hesaplandı: {Metot}, {SegmentCount} segment, toplam {Hacim:F2} m³",
                metot, sonuc.Segmentler.Count, toplamHacim);

            return sonuc;
        }

        public List<BrucknerNoktasi> BrucknerHesapla(HacimHesapSonucu sonuc)
        {
            var bruckner = new List<BrucknerNoktasi>();
            if (sonuc == null || sonuc.Segmentler == null || sonuc.Segmentler.Count == 0)
                return bruckner;

            double kumulatif = 0;
            bruckner.Add(new BrucknerNoktasi
            {
                Istasyon = sonuc.Segmentler[0].Istasyon1,
                KumulatifHacim = 0
            });

            foreach (var seg in sonuc.Segmentler)
            {
                kumulatif += seg.Hacim;
                bruckner.Add(new BrucknerNoktasi
                {
                    Istasyon = seg.Istasyon2,
                    KumulatifHacim = kumulatif
                });
            }

            return bruckner;
        }

        public double EnkesitAlanHesapla(List<Autodesk.AutoCAD.Geometry.Point2d> profilNoktalari)
        {
            if (profilNoktalari == null || profilNoktalari.Count < 3)
                return 0;

            // Shoelace formülü
            double alan = 0;
            int n = profilNoktalari.Count;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                alan += profilNoktalari[i].X * profilNoktalari[j].Y;
                alan -= profilNoktalari[j].X * profilNoktalari[i].Y;
            }

            return Math.Abs(alan) / 2.0;
        }
    }
}
