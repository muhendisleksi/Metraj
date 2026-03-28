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

        public KesitAlanHesapService(IEnKesitAlanService enKesitAlanService)
        {
            _enKesitAlanService = enKesitAlanService;
        }

        public List<AlanHesapSonucu> AlanHesapla(KesitGrubu kesit)
        {
            var sonuclar = new List<AlanHesapSonucu>();

            var zemin = kesit.Zemin;
            var siyirmaTaban = kesit.SiyirmaTaban;
            var projeKotu = kesit.ProjeKotu;
            var ustyapiAlt = kesit.UstyapiAltKotu;

            if (zemin != null && siyirmaTaban != null)
            {
                double siyirmaAlani = IkiCizgiArasiAlanHesapla(zemin.Noktalar, siyirmaTaban.Noktalar);
                sonuclar.Add(new AlanHesapSonucu
                {
                    MalzemeAdi = "Sıyırma",
                    Alan = siyirmaAlani,
                    UstCizgiRolu = CizgiRolu.Zemin,
                    AltCizgiRolu = CizgiRolu.SiyirmaTaban,
                    Aciklama = "Zemin - Sıyırma tabanı arası"
                });
            }

            if (siyirmaTaban != null && ustyapiAlt != null)
                HesaplaYarmaDolgu(siyirmaTaban, ustyapiAlt, zemin, sonuclar);

            HesaplaUstyapiTabakalari(kesit, sonuclar);

            kesit.HesaplananAlanlar = sonuclar;
            return sonuclar;
        }

        public void TopluAlanHesapla(List<KesitGrubu> kesitler)
        {
            foreach (var kesit in kesitler)
                AlanHesapla(kesit);

            LoggingService.Info($"Toplu alan hesabı: {kesitler.Count} kesit hesaplandı");
        }

        private void HesaplaYarmaDolgu(CizgiTanimi siyirmaTaban, CizgiTanimi ustyapiAlt, CizgiTanimi zemin, List<AlanHesapSonucu> sonuclar)
        {
            double minX = Math.Max(siyirmaTaban.Noktalar.Min(p => p.X), ustyapiAlt.Noktalar.Min(p => p.X));
            double maxX = Math.Min(siyirmaTaban.Noktalar.Max(p => p.X), ustyapiAlt.Noktalar.Max(p => p.X));

            double? kesisimX = KesisimXBul(siyirmaTaban.Noktalar, ustyapiAlt.Noktalar, minX, maxX);

            if (kesisimX.HasValue)
            {
                double yarmaAlani = BolgeAlanHesapla(siyirmaTaban.Noktalar, ustyapiAlt.Noktalar, minX, kesisimX.Value, true);
                double dolguAlani = BolgeAlanHesapla(ustyapiAlt.Noktalar, siyirmaTaban.Noktalar, kesisimX.Value, maxX, true);

                if (yarmaAlani > 0.0001)
                {
                    sonuclar.Add(new AlanHesapSonucu
                    {
                        MalzemeAdi = "Yarma",
                        Alan = yarmaAlani,
                        UstCizgiRolu = CizgiRolu.SiyirmaTaban,
                        AltCizgiRolu = CizgiRolu.UstyapiAltKotu,
                        Aciklama = "Sıyırma tabanı > Üstyapı alt kotu bölgesi"
                    });
                }

                if (dolguAlani > 0.0001)
                {
                    sonuclar.Add(new AlanHesapSonucu
                    {
                        MalzemeAdi = "Dolgu",
                        Alan = dolguAlani,
                        UstCizgiRolu = CizgiRolu.UstyapiAltKotu,
                        AltCizgiRolu = CizgiRolu.SiyirmaTaban,
                        Aciklama = "Üstyapı alt kotu > Sıyırma tabanı bölgesi"
                    });
                }

                // B.T. Yerine Konan / Konmayan
                if (zemin != null)
                {
                    var siyirmaAlani = sonuclar.FirstOrDefault(s => s.MalzemeAdi == "Sıyırma");
                    if (siyirmaAlani != null && siyirmaAlani.Alan > 0)
                    {
                        double siyirmaDolguBolgesi = BolgeAlanHesapla(zemin.Noktalar, CizgiTanimi(CizgiRolu.SiyirmaTaban, siyirmaTaban).Noktalar, kesisimX.Value, maxX, false);
                        double siyirmaYarmaBolgesi = siyirmaAlani.Alan - siyirmaDolguBolgesi;

                        if (siyirmaDolguBolgesi > 0.0001)
                        {
                            sonuclar.Add(new AlanHesapSonucu
                            {
                                MalzemeAdi = "B.T. Yerine Konan",
                                Alan = siyirmaDolguBolgesi,
                                UstCizgiRolu = CizgiRolu.Zemin,
                                AltCizgiRolu = CizgiRolu.SiyirmaTaban,
                                Aciklama = "Dolgu bölgesindeki sıyırma"
                            });
                        }

                        if (siyirmaYarmaBolgesi > 0.0001)
                        {
                            sonuclar.Add(new AlanHesapSonucu
                            {
                                MalzemeAdi = "B.T. Yerine Konmayan",
                                Alan = siyirmaYarmaBolgesi,
                                UstCizgiRolu = CizgiRolu.Zemin,
                                AltCizgiRolu = CizgiRolu.SiyirmaTaban,
                                Aciklama = "Yarma bölgesindeki sıyırma"
                            });
                        }
                    }
                }
            }
            else
            {
                double tamAlan = IkiCizgiArasiAlanHesapla(siyirmaTaban.Noktalar, ustyapiAlt.Noktalar);
                double siyirmaOrt = siyirmaTaban.OrtalamaY;
                double ustyapiOrt = ustyapiAlt.OrtalamaY;

                string malzeme = siyirmaOrt > ustyapiOrt ? "Yarma" : "Dolgu";
                var ust = siyirmaOrt > ustyapiOrt ? CizgiRolu.SiyirmaTaban : CizgiRolu.UstyapiAltKotu;
                var alt = siyirmaOrt > ustyapiOrt ? CizgiRolu.UstyapiAltKotu : CizgiRolu.SiyirmaTaban;

                if (tamAlan > 0.0001)
                {
                    sonuclar.Add(new AlanHesapSonucu
                    {
                        MalzemeAdi = malzeme,
                        Alan = tamAlan,
                        UstCizgiRolu = ust,
                        AltCizgiRolu = alt,
                        Aciklama = $"{malzeme} - tam bölge"
                    });
                }
            }
        }

        private CizgiTanimi CizgiTanimi(CizgiRolu rol, CizgiTanimi kaynak)
        {
            return kaynak;
        }

        private void HesaplaUstyapiTabakalari(KesitGrubu kesit, List<AlanHesapSonucu> sonuclar)
        {
            var tabakalar = new[]
            {
                (ust: CizgiRolu.ProjeKotu, alt: CizgiRolu.AsinmaTaban, ad: "Aşınma"),
                (ust: CizgiRolu.AsinmaTaban, alt: CizgiRolu.BinderTaban, ad: "Binder"),
                (ust: CizgiRolu.BinderTaban, alt: CizgiRolu.BitumluTemelTaban, ad: "Bitümlü Temel"),
                (ust: CizgiRolu.BitumluTemelTaban, alt: CizgiRolu.PlentmiksTaban, ad: "Plentmiks"),
                (ust: CizgiRolu.PlentmiksTaban, alt: CizgiRolu.AltTemelTaban, ad: "Alttemel"),
                (ust: CizgiRolu.AltTemelTaban, alt: CizgiRolu.KirmatasTaban, ad: "Kırmataş"),
            };

            foreach (var (ust, alt, ad) in tabakalar)
            {
                var ustCizgi = kesit.Cizgiler.FirstOrDefault(c => c.Rol == ust);
                var altCizgi = kesit.Cizgiler.FirstOrDefault(c => c.Rol == alt);

                if (ustCizgi == null || altCizgi == null) continue;

                double alan = IkiCizgiArasiAlanHesapla(ustCizgi.Noktalar, altCizgi.Noktalar);
                if (alan > 0.0001)
                {
                    sonuclar.Add(new AlanHesapSonucu
                    {
                        MalzemeAdi = ad,
                        Alan = alan,
                        UstCizgiRolu = ust,
                        AltCizgiRolu = alt,
                        Aciklama = $"{ad} tabakası"
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
                {
                    double kesisimX = IkiliAramaKesisim(cizgi1, cizgi2, x - adim, x);
                    return kesisimX;
                }

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
                double y1 = _enKesitAlanService.InterpolateY(cizgi1, ortaX);
                double y2 = _enKesitAlanService.InterpolateY(cizgi2, ortaX);
                double fark = y1 - y2;

                if (Math.Abs(fark) < 1e-6) return ortaX;

                double y1Sol = _enKesitAlanService.InterpolateY(cizgi1, solX);
                double y2Sol = _enKesitAlanService.InterpolateY(cizgi2, solX);
                double solFark = y1Sol - y2Sol;

                if (solFark * fark < 0)
                    sagX = ortaX;
                else
                    solX = ortaX;
            }

            return (solX + sagX) / 2;
        }

        private double BolgeAlanHesapla(List<Point2d> ustNoktalar, List<Point2d> altNoktalar, double minX, double maxX, bool sadecePozitif)
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
