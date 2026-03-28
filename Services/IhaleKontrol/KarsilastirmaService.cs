using System;
using System.Collections.Generic;
using System.Linq;
using Metraj.Models;
using Metraj.Models.IhaleKontrol;
using Metraj.Services.IhaleKontrol.Interfaces;

namespace Metraj.Services.IhaleKontrol
{
    public class KarsilastirmaService : IKarsilastirmaService
    {
        public List<KesitKarsilastirma> Karsilastir(
            List<TabloKesitVerisi> tabloVerileri,
            List<GeometrikKesitVerisi> geometrikVeriler,
            double uyariTolerans = 3.0,
            double hataTolerans = 10.0,
            double mutlakTolerans = 0.1)
        {
            var sonuclar = new List<KesitKarsilastirma>();

            foreach (var tablo in tabloVerileri)
            {
                // Aynı istasyondaki geometrik veriyi bul
                var geometrik = geometrikVeriler
                    .FirstOrDefault(g => Math.Abs(g.Istasyon - tablo.Istasyon) < 0.5);

                var karsilastirma = new KesitKarsilastirma
                {
                    Istasyon = tablo.Istasyon,
                    IstasyonMetni = tablo.IstasyonMetni
                };

                double toplamTablo = 0;
                double toplamGeometrik = 0;

                foreach (var malzeme in tablo.MalzemeAlanlari)
                {
                    var mk = new MalzemeKarsilastirma
                    {
                        MalzemeAdi = malzeme.NormalizeMalzemeAdi,
                        TabloDegeri = malzeme.Alan
                    };

                    if (geometrik != null)
                    {
                        var geoMalzeme = geometrik.TabakaAlanlari
                            .FirstOrDefault(t => t.MalzemeAdi == malzeme.NormalizeMalzemeAdi);

                        if (geoMalzeme != null)
                        {
                            mk.GeometrikDeger = geoMalzeme.Alan;
                            mk.GeometrikHesapYapildi = true;
                            mk.Tahmini = geoMalzeme.Tahmini;
                            mk.Fark = mk.GeometrikDeger - mk.TabloDegeri;
                            mk.FarkYuzde = mk.TabloDegeri > 0
                                ? (mk.Fark / mk.TabloDegeri) * 100.0
                                : 0;
                            mk.Durum = DurumBelirle(mk.FarkYuzde, mk.Fark,
                                uyariTolerans, hataTolerans, mutlakTolerans);
                            toplamGeometrik += geoMalzeme.Alan;
                        }
                        else
                        {
                            mk.GeometrikHesapYapildi = false;
                            mk.Durum = KontrolDurumu.DogrulamaYok;
                        }
                    }
                    else
                    {
                        mk.GeometrikHesapYapildi = false;
                        mk.Durum = KontrolDurumu.DogrulamaYok;
                    }

                    toplamTablo += malzeme.Alan;
                    karsilastirma.Malzemeler.Add(mk);
                }

                karsilastirma.ToplamTabloDegeri = toplamTablo;
                karsilastirma.ToplamGeometrikDeger = toplamGeometrik;
                karsilastirma.ToplamFark = toplamGeometrik - toplamTablo;
                karsilastirma.ToplamFarkYuzde = toplamTablo > 0
                    ? (karsilastirma.ToplamFark / toplamTablo) * 100.0
                    : 0;

                // Genel durum: en kötü malzeme durumunu al
                if (karsilastirma.Malzemeler.Any(m => m.Durum == KontrolDurumu.Hata))
                    karsilastirma.Durum = KontrolDurumu.Hata;
                else if (karsilastirma.Malzemeler.Any(m => m.Durum == KontrolDurumu.Uyari))
                    karsilastirma.Durum = KontrolDurumu.Uyari;
                else if (karsilastirma.Malzemeler.All(m => m.Durum == KontrolDurumu.DogrulamaYok))
                    karsilastirma.Durum = KontrolDurumu.DogrulamaYok;
                else
                    karsilastirma.Durum = KontrolDurumu.OK;

                sonuclar.Add(karsilastirma);
            }

            LoggingService.Info("Karşılaştırma tamamlandı: {Toplam} kesit, {Hata} hata, {Uyari} uyarı",
                sonuclar.Count,
                sonuclar.Count(s => s.Durum == KontrolDurumu.Hata),
                sonuclar.Count(s => s.Durum == KontrolDurumu.Uyari));

            return sonuclar;
        }

        public KubajKarsilastirma KubajKarsilastir(
            List<TabloKesitVerisi> tabloVerileri,
            List<GeometrikKesitVerisi> geometrikVeriler,
            HacimMetoduSecimi metot = HacimMetoduSecimi.OrtalamaAlan)
        {
            var sonuc = new KubajKarsilastirma();

            // Her malzeme için tüm kesitler üzerinden kübaj hesapla
            var tumMalzemeler = tabloVerileri
                .SelectMany(t => t.MalzemeAlanlari.Select(m => m.NormalizeMalzemeAdi))
                .Distinct()
                .ToList();

            var hacimMetodu = metot == HacimMetoduSecimi.Prismoidal
                ? HacimMetodu.Prismoidal
                : HacimMetodu.OrtalamaAlan;

            foreach (string malzemeAdi in tumMalzemeler)
            {
                // Tablo verilerinden kübaj
                double ihaleHacmi = KubajHesapla(tabloVerileri, malzemeAdi, hacimMetodu,
                    t => t.MalzemeAlanlari.FirstOrDefault(m => m.NormalizeMalzemeAdi == malzemeAdi)?.Alan ?? 0);

                // Geometrik verilerden kübaj
                double hesapHacmi = KubajHesapla(tabloVerileri, malzemeAdi, hacimMetodu,
                    t =>
                    {
                        var geo = geometrikVeriler
                            .FirstOrDefault(g => Math.Abs(g.Istasyon - t.Istasyon) < 0.5);
                        return geo?.TabakaAlanlari
                            .FirstOrDefault(ta => ta.MalzemeAdi == malzemeAdi)?.Alan ?? 0;
                    });

                double fark = hesapHacmi - ihaleHacmi;
                double farkYuzde = ihaleHacmi > 0 ? (fark / ihaleHacmi) * 100.0 : 0;

                sonuc.MalzemeKubajlari.Add(new MalzemeKubajKarsilastirma
                {
                    MalzemeAdi = malzemeAdi,
                    IhaleHacmi = Math.Round(ihaleHacmi, 2),
                    HesapHacmi = Math.Round(hesapHacmi, 2),
                    Fark = Math.Round(fark, 2),
                    FarkYuzde = Math.Round(farkYuzde, 2),
                    Durum = DurumBelirle(farkYuzde, fark, 3.0, 10.0, 1.0)
                });
            }

            return sonuc;
        }

        private double KubajHesapla(List<TabloKesitVerisi> tabloVerileri,
            string malzemeAdi, HacimMetodu metot, Func<TabloKesitVerisi, double> alanGetir)
        {
            var sirali = tabloVerileri.OrderBy(t => t.Istasyon).ToList();
            double toplamHacim = 0;

            for (int i = 0; i < sirali.Count - 1; i++)
            {
                double alan1 = alanGetir(sirali[i]);
                double alan2 = alanGetir(sirali[i + 1]);
                double mesafe = sirali[i + 1].Istasyon - sirali[i].Istasyon;

                if (mesafe <= 0) continue;

                toplamHacim += HacimFormulleri.Hesapla(alan1, alan2, mesafe, metot);
            }

            return toplamHacim;
        }

        private KontrolDurumu DurumBelirle(double farkYuzde, double mutlakFark,
            double uyariTolerans, double hataTolerans, double mutlakToleransLimit)
        {
            double absFarkYuzde = Math.Abs(farkYuzde);
            double absMutlakFark = Math.Abs(mutlakFark);

            // Küçük mutlak farklarda tolerans uygula (ince tabakalar için)
            if (absMutlakFark <= mutlakToleransLimit)
                return KontrolDurumu.OK;

            if (absFarkYuzde >= hataTolerans)
                return KontrolDurumu.Hata;
            if (absFarkYuzde >= uyariTolerans)
                return KontrolDurumu.Uyari;

            return KontrolDurumu.OK;
        }
    }
}
