using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using Metraj.Models.YolEnkesit;
using Metraj.Services.Interfaces;

namespace Metraj.Services.YolEnkesit
{
    public class KesitAlanHesapService : IKesitAlanHesapService
    {
        private readonly IEnKesitAlanService _enKesitAlanService;
        private int _logSayaci;

        public KesitAlanHesapService(IEnKesitAlanService enKesitAlanService)
        {
            _enKesitAlanService = enKesitAlanService;
        }

        public List<AlanHesapSonucu> AlanHesapla(KesitGrubu kesit)
        {
            var sonuclar = new List<AlanHesapSonucu>();
            bool detayliLog = _logSayaci < 3;

            if (detayliLog)
            {
                LoggingService.Info($"=== ALAN HESAP DETAY: {kesit.Anchor?.IstasyonMetni} ===");
                foreach (var c in kesit.Cizgiler.Where(c => c.Rol != CizgiRolu.Tanimsiz && c.Rol != CizgiRolu.CerceveCizgisi && c.Rol != CizgiRolu.GridCizgisi))
                {
                    double cMinX = c.Noktalar.Min(p => p.X);
                    double cMaxX = c.Noktalar.Max(p => p.X);
                    double cMinY = c.Noktalar.Min(p => p.Y);
                    double cMaxY = c.Noktalar.Max(p => p.Y);
                    LoggingService.Info($"  {c.Rol}: {c.LayerAdi}, {c.Noktalar.Count} nokta, X=[{cMinX:F2}..{cMaxX:F2}], Y=[{cMinY:F2}..{cMaxY:F2}]");
                }
            }

            var zemin = kesit.Zemin;
            var siyirmaTaban = kesit.SiyirmaTaban;
            var projeKotu = kesit.ProjeKotu;
            var ustyapiAlt = kesit.UstyapiAltKotu;

            if (zemin != null && siyirmaTaban != null)
            {
                double siyirmaAlani = IkiCizgiArasiAlanHesapla(zemin.Noktalar, siyirmaTaban.Noktalar);
                if (detayliLog) LoggingService.Info($"  Siyirma alani: {siyirmaAlani:F4} m2");
                sonuclar.Add(new AlanHesapSonucu
                {
                    MalzemeAdi = "Siyirma",
                    Alan = siyirmaAlani,
                    UstCizgiRolu = CizgiRolu.Zemin,
                    AltCizgiRolu = CizgiRolu.SiyirmaTaban,
                    Aciklama = "Zemin - Siyirma tabani arasi"
                });
            }

            if (siyirmaTaban != null && ustyapiAlt != null)
                HesaplaYarmaDolgu(siyirmaTaban, ustyapiAlt, zemin, sonuclar, detayliLog);

            HesaplaUstyapiTabakalari(kesit, sonuclar, detayliLog);

            kesit.HesaplananAlanlar = sonuclar;

            if (detayliLog)
            {
                LoggingService.Info($"  TOPLAM: {sonuclar.Count} malzeme hesaplandi");
                foreach (var s in sonuclar)
                    LoggingService.Info($"    {s.MalzemeAdi} = {s.Alan:F4} m2");
            }

            _logSayaci++;
            return sonuclar;
        }

        public void TopluAlanHesapla(List<KesitGrubu> kesitler)
        {
            _logSayaci = 0;
            foreach (var kesit in kesitler)
                AlanHesapla(kesit);

            LoggingService.Info($"Toplu alan hesabi: {kesitler.Count} kesit hesaplandi");
        }

        private void HesaplaYarmaDolgu(CizgiTanimi siyirmaTaban, CizgiTanimi ustyapiAlt, CizgiTanimi zemin, List<AlanHesapSonucu> sonuclar, bool detayliLog)
        {
            double minX = Math.Max(siyirmaTaban.Noktalar.Min(p => p.X), ustyapiAlt.Noktalar.Min(p => p.X));
            double maxX = Math.Min(siyirmaTaban.Noktalar.Max(p => p.X), ustyapiAlt.Noktalar.Max(p => p.X));

            if (detayliLog) LoggingService.Info($"  Yarma/Dolgu X araligi: [{minX:F2}..{maxX:F2}]");

            double? kesisimX = KesisimXBul(siyirmaTaban.Noktalar, ustyapiAlt.Noktalar, minX, maxX);

            if (kesisimX.HasValue)
            {
                if (detayliLog) LoggingService.Info($"  Kesisim X: {kesisimX:F2}");

                double yarmaAlani = BolgeAlanHesapla(siyirmaTaban.Noktalar, ustyapiAlt.Noktalar, minX, kesisimX.Value);
                double dolguAlani = BolgeAlanHesapla(ustyapiAlt.Noktalar, siyirmaTaban.Noktalar, kesisimX.Value, maxX);

                if (yarmaAlani > 0.0001)
                    sonuclar.Add(new AlanHesapSonucu { MalzemeAdi = "Yarma", Alan = yarmaAlani, UstCizgiRolu = CizgiRolu.SiyirmaTaban, AltCizgiRolu = CizgiRolu.UstyapiAltKotu, Aciklama = "Siyirma tabani > Ustyapi alt kotu bolgesi" });

                if (dolguAlani > 0.0001)
                    sonuclar.Add(new AlanHesapSonucu { MalzemeAdi = "Dolgu", Alan = dolguAlani, UstCizgiRolu = CizgiRolu.UstyapiAltKotu, AltCizgiRolu = CizgiRolu.SiyirmaTaban, Aciklama = "Ustyapi alt kotu > Siyirma tabani bolgesi" });
            }
            else
            {
                double tamAlan = IkiCizgiArasiAlanHesapla(siyirmaTaban.Noktalar, ustyapiAlt.Noktalar);
                double siyirmaOrt = siyirmaTaban.OrtalamaY;
                double ustyapiOrt = ustyapiAlt.OrtalamaY;
                string malzeme = siyirmaOrt > ustyapiOrt ? "Yarma" : "Dolgu";

                if (tamAlan > 0.0001)
                    sonuclar.Add(new AlanHesapSonucu { MalzemeAdi = malzeme, Alan = tamAlan, UstCizgiRolu = siyirmaOrt > ustyapiOrt ? CizgiRolu.SiyirmaTaban : CizgiRolu.UstyapiAltKotu, AltCizgiRolu = siyirmaOrt > ustyapiOrt ? CizgiRolu.UstyapiAltKotu : CizgiRolu.SiyirmaTaban, Aciklama = $"{malzeme} - tam bolge" });
            }
        }

        private void HesaplaUstyapiTabakalari(KesitGrubu kesit, List<AlanHesapSonucu> sonuclar, bool detayliLog)
        {
            var tabakalar = new[]
            {
                (ust: CizgiRolu.ProjeKotu, alt: CizgiRolu.AsinmaTaban, ad: "Asinma"),
                (ust: CizgiRolu.AsinmaTaban, alt: CizgiRolu.BinderTaban, ad: "Binder"),
                (ust: CizgiRolu.BinderTaban, alt: CizgiRolu.BitumluTemelTaban, ad: "Bitumlu Temel"),
                (ust: CizgiRolu.BitumluTemelTaban, alt: CizgiRolu.PlentmiksTaban, ad: "Plentmiks"),
                (ust: CizgiRolu.PlentmiksTaban, alt: CizgiRolu.AltTemelTaban, ad: "Alttemel"),
                (ust: CizgiRolu.AltTemelTaban, alt: CizgiRolu.KirmatasTaban, ad: "Kirmatas"),
            };

            foreach (var (ust, alt, ad) in tabakalar)
            {
                var ustCizgi = kesit.Cizgiler.FirstOrDefault(c => c.Rol == ust);
                var altCizgi = kesit.Cizgiler.FirstOrDefault(c => c.Rol == alt);

                if (ustCizgi == null || altCizgi == null) continue;

                double alan = IkiCizgiArasiAlanHesapla(ustCizgi.Noktalar, altCizgi.Noktalar);
                if (detayliLog) LoggingService.Info($"  {ad}: {alan:F4} m2 ({ust} -> {alt})");

                if (alan > 0.0001)
                {
                    sonuclar.Add(new AlanHesapSonucu
                    {
                        MalzemeAdi = ad,
                        Alan = alan,
                        UstCizgiRolu = ust,
                        AltCizgiRolu = alt,
                        Aciklama = $"{ad} tabakasi"
                    });
                }
            }
        }

        private double IkiCizgiArasiAlanHesapla(List<Point2d> ustNoktalar, List<Point2d> altNoktalar)
        {
            double minX = Math.Max(ustNoktalar.Min(p => p.X), altNoktalar.Min(p => p.X));
            double maxX = Math.Min(ustNoktalar.Max(p => p.X), altNoktalar.Max(p => p.X));

            if (maxX <= minX) return 0;

            var ustKesik = _enKesitAlanService.ClipToXRange(ustNoktalar, minX, maxX);
            var altKesik = _enKesitAlanService.ClipToXRange(altNoktalar, minX, maxX);

            var polygon = new List<Point2d>();
            polygon.AddRange(ustKesik.OrderBy(p => p.X));
            polygon.AddRange(altKesik.OrderByDescending(p => p.X));

            return _enKesitAlanService.ShoelaceAlan(polygon);
        }

        private double? KesisimXBul(List<Point2d> cizgi1, List<Point2d> cizgi2, double minX, double maxX)
        {
            int adimSayisi = 100;
            double adim = (maxX - minX) / adimSayisi;

            double oncekiFark = 0;
            bool ilk = true;

            for (double x = minX; x <= maxX; x += adim)
            {
                double y1 = _enKesitAlanService.InterpolateY(cizgi1, x);
                double y2 = _enKesitAlanService.InterpolateY(cizgi2, x);
                double fark = y1 - y2;

                if (!ilk && oncekiFark * fark < 0)
                    return IkiliAramaKesisim(cizgi1, cizgi2, x - adim, x);

                oncekiFark = fark;
                ilk = false;
            }

            return null;
        }

        private double IkiliAramaKesisim(List<Point2d> cizgi1, List<Point2d> cizgi2, double solX, double sagX)
        {
            for (int i = 0; i < 50; i++)
            {
                double ortaX = (solX + sagX) / 2;
                double fark = _enKesitAlanService.InterpolateY(cizgi1, ortaX) - _enKesitAlanService.InterpolateY(cizgi2, ortaX);

                if (Math.Abs(fark) < 1e-6) return ortaX;

                double solFark = _enKesitAlanService.InterpolateY(cizgi1, solX) - _enKesitAlanService.InterpolateY(cizgi2, solX);
                if (solFark * fark < 0) sagX = ortaX;
                else solX = ortaX;
            }

            return (solX + sagX) / 2;
        }

        private double BolgeAlanHesapla(List<Point2d> ustNoktalar, List<Point2d> altNoktalar, double minX, double maxX)
        {
            if (maxX <= minX) return 0;

            var ustKesik = _enKesitAlanService.ClipToXRange(ustNoktalar, minX, maxX);
            var altKesik = _enKesitAlanService.ClipToXRange(altNoktalar, minX, maxX);

            var polygon = new List<Point2d>();
            polygon.AddRange(ustKesik.OrderBy(p => p.X));
            polygon.AddRange(altKesik.OrderByDescending(p => p.X));

            return _enKesitAlanService.ShoelaceAlan(polygon);
        }
    }
}
