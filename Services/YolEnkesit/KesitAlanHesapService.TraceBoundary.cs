using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    /// <summary>
    /// TraceBoundary tabanli alan hesabi.
    /// DWG'deki gercek cizimler uzerinde calisir — iki cizgi arasinda sample point uretip
    /// AutoCAD'in TraceBoundary metoduyla kapali bolge alanini bulur.
    /// Shoelace yonteminin yerine birincil hesap metodu olarak kullanilir.
    /// </summary>
    public partial class KesitAlanHesapService
    {
        // =============== ANA GIRIS NOKTASI ===============

        /// <summary>
        /// Alan hesabi: Her malzeme icin TraceBoundary oncelikli, bulunamazsa Shoelace fallback.
        /// Iki yontemin sonuclarini per-malzeme birlestirerek en iyi kapsami saglar.
        /// </summary>
        public List<AlanHesapSonucu> AlanHesapla(KesitGrubu kesit)
        {
            var tbSonuclar = TraceBoundaryAlanHesapla(kesit);
            var shSonuclar = ShoelaceAlanHesapla(kesit);

            // Per-malzeme birlestirme: TB varsa TB, yoksa Shoelace
            var tbDict = new Dictionary<string, AlanHesapSonucu>();
            foreach (var s in tbSonuclar)
                tbDict[s.MalzemeAdi] = s;

            var shDict = new Dictionary<string, AlanHesapSonucu>();
            foreach (var s in shSonuclar)
                shDict[s.MalzemeAdi] = s;

            var tumMalzemeler = new HashSet<string>(tbDict.Keys);
            foreach (var k in shDict.Keys)
                tumMalzemeler.Add(k);

            var birlesik = new List<AlanHesapSonucu>();
            bool detayliLog = _logSayaci < 3;

            foreach (var mal in tumMalzemeler)
            {
                if (tbDict.TryGetValue(mal, out var tb))
                {
                    birlesik.Add(tb);
                    if (detayliLog) LoggingService.Info($"  {mal}: TB={tb.Alan:F4} m2");
                }
                else if (shDict.TryGetValue(mal, out var sh))
                {
                    sh.Aciklama += " [Shoelace fallback]";
                    birlesik.Add(sh);
                    if (detayliLog) LoggingService.Info($"  {mal}: Shoelace={sh.Alan:F4} m2 [fallback]");
                }
            }

            if (detayliLog)
                LoggingService.Info($"Birlesik sonuc: {tbDict.Count} TB + {birlesik.Count - tbDict.Count} Shoelace = {birlesik.Count} malzeme");

            kesit.HesaplananAlanlar = birlesik;
            _logSayaci++;
            return birlesik;
        }

        // =============== TRACE BOUNDARY ===============

        /// <summary>
        /// TraceBoundary kullanarak tum malzemeler icin alan hesaplar.
        /// Her malzeme cifti icin iki cizgi arasinda sample point uretip
        /// AutoCAD Editor.TraceBoundary ile kapali bolge alani bulur.
        /// </summary>
        public List<AlanHesapSonucu> TraceBoundaryAlanHesapla(KesitGrubu kesit)
        {
            var sonuclar = new List<AlanHesapSonucu>();

            if (!kesit.CL_X.HasValue)
            {
                LoggingService.Warning($"TraceBoundary: {kesit.Anchor?.IstasyonMetni} — CL_X yok, atliyor");
                return sonuclar;
            }

            double clX = kesit.CL_X.Value;
            bool detayliLog = _logSayaci < 3;

            if (detayliLog)
                LoggingService.Info($"=== TRACE BOUNDARY ALAN HESAP: {kesit.Anchor?.IstasyonMetni}, CL_X={clX:F2} ===");

            // 1. Siyirma: Zemin - SiyirmaTaban
            TBMalzemeHesapla(kesit, CizgiRolu.Zemin, CizgiRolu.SiyirmaTaban, "Siyirma", clX, sonuclar, detayliLog);

            // 2. Ustyapi tabakalari
            TBMalzemeHesapla(kesit, CizgiRolu.ProjeKotu, CizgiRolu.AsinmaTaban, "Asinma", clX, sonuclar, detayliLog);
            TBMalzemeHesapla(kesit, CizgiRolu.AsinmaTaban, CizgiRolu.BinderTaban, "Binder", clX, sonuclar, detayliLog);
            TBMalzemeHesapla(kesit, CizgiRolu.BinderTaban, CizgiRolu.BitumluTemelTaban, "Bitumlu Temel", clX, sonuclar, detayliLog);
            TBMalzemeHesapla(kesit, CizgiRolu.BitumluTemelTaban, CizgiRolu.PlentmiksTaban, "Plentmiks", clX, sonuclar, detayliLog);
            TBMalzemeHesapla(kesit, CizgiRolu.PlentmiksTaban, CizgiRolu.AltTemelTaban, "Alttemel", clX, sonuclar, detayliLog);
            TBMalzemeHesapla(kesit, CizgiRolu.AltTemelTaban, CizgiRolu.KirmatasTaban, "Kirmatas", clX, sonuclar, detayliLog);

            // 3. Yarma / Dolgu (ozel durum)
            var siyirmaNkt = RolNoktalariniAl(kesit, CizgiRolu.SiyirmaTaban);
            var ustyapiAltNkt = UstyapiAltNoktalariniAl(kesit);

            if (siyirmaNkt != null && ustyapiAltNkt != null)
                TBYarmaDolgu(siyirmaNkt, ustyapiAltNkt, clX, sonuclar, detayliLog);

            if (detayliLog)
            {
                LoggingService.Info($"  TraceBoundary TOPLAM: {sonuclar.Count} malzeme");
                foreach (var s in sonuclar)
                    LoggingService.Info($"    {s.MalzemeAdi} = {s.Alan:F4} m2");
            }

            return sonuclar;
        }

        // =============== MALZEME HESAP ===============

        /// <summary>
        /// Tek bir malzeme cifti icin TraceBoundary ile alan hesaplar.
        /// CL_X'te sample point uretir; bulunamazsa CL+-2m dener.
        /// </summary>
        private void TBMalzemeHesapla(
            KesitGrubu kesit, CizgiRolu ustRol, CizgiRolu altRol,
            string malzemeAdi, double clX,
            List<AlanHesapSonucu> sonuclar, bool detayliLog)
        {
            var ustNkt = RolNoktalariniAl(kesit, ustRol);
            var altNkt = RolNoktalariniAl(kesit, altRol);
            if (ustNkt == null || altNkt == null) return;

            double? alan = SampleNoktaIleTraceBoundary(ustNkt, altNkt, clX, detayliLog, malzemeAdi);

            if (alan.HasValue && alan.Value > 0.0001)
            {
                sonuclar.Add(new AlanHesapSonucu
                {
                    MalzemeAdi = malzemeAdi,
                    Alan = alan.Value,
                    UstCizgiRolu = ustRol,
                    AltCizgiRolu = altRol,
                    Aciklama = $"TraceBoundary: {malzemeAdi}"
                });
            }
        }

        /// <summary>
        /// Iki cizgi arasinda sample point uretip TraceBoundary cagirir.
        /// Sirasiyla CL_X, CL_X-2, CL_X+2 dener.
        /// </summary>
        private double? SampleNoktaIleTraceBoundary(
            List<Point2d> ustNkt, List<Point2d> altNkt,
            double clX, bool detayliLog, string malzemeAdi)
        {
            double[] denemeXleri = { clX, clX - 2.0, clX + 2.0 };

            double minXOrtak = Math.Max(ustNkt.Min(p => p.X), altNkt.Min(p => p.X));
            double maxXOrtak = Math.Min(ustNkt.Max(p => p.X), altNkt.Max(p => p.X));

            foreach (double x in denemeXleri)
            {
                if (x < minXOrtak || x > maxXOrtak) continue;

                double ustY = _enKesitAlanService.InterpolateY(ustNkt, x);
                double altY = _enKesitAlanService.InterpolateY(altNkt, x);

                // Cizgiler ayni noktada — alan yok
                if (Math.Abs(ustY - altY) < 0.001) continue;

                double ortaY = (ustY + altY) / 2.0;
                var samplePoint = new Point3d(x, ortaY, 0);
                double? alan = TraceBoundaryAlanAl(samplePoint);

                if (detayliLog)
                    LoggingService.Info($"  {malzemeAdi}: sample=({x:F2},{ortaY:F2}), alan={alan?.ToString("F4") ?? "null"}");

                if (alan.HasValue && alan.Value > 0.0001)
                    return alan.Value;
            }

            return null;
        }

        // =============== YARMA / DOLGU ===============

        /// <summary>
        /// Yarma/Dolgu icin ozel TraceBoundary hesabi.
        /// Tek bolgeli (tamami yarma veya dolgu) ve karisik (sol yarma, sag dolgu) durumlarini isle.
        /// </summary>
        private void TBYarmaDolgu(
            List<Point2d> siyirmaNkt, List<Point2d> ustyapiAltNkt,
            double clX, List<AlanHesapSonucu> sonuclar, bool detayliLog)
        {
            double minXOrtak = Math.Max(siyirmaNkt.Min(p => p.X), ustyapiAltNkt.Min(p => p.X));
            double maxXOrtak = Math.Min(siyirmaNkt.Max(p => p.X), ustyapiAltNkt.Max(p => p.X));

            if (maxXOrtak <= minXOrtak) return;

            // CL noktasinda dene — iki cizgi arasindaki bolge tekse burada bulunur
            double clSiyirmaY = _enKesitAlanService.InterpolateY(siyirmaNkt, clX);
            double clUstyapiY = _enKesitAlanService.InterpolateY(ustyapiAltNkt, clX);
            double clOrtaY = (clSiyirmaY + clUstyapiY) / 2.0;

            var clSample = new Point3d(clX, clOrtaY, 0);
            double? clAlan = TraceBoundaryAlanAl(clSample);

            if (clAlan.HasValue && clAlan.Value > 0.0001)
            {
                // Tek bolge — tamami yarma veya dolgu
                bool yarma = siyirmaNkt.Average(p => p.Y) > ustyapiAltNkt.Average(p => p.Y);
                string malzeme = yarma ? "Yarma" : "Dolgu";

                sonuclar.Add(new AlanHesapSonucu
                {
                    MalzemeAdi = malzeme,
                    Alan = clAlan.Value,
                    UstCizgiRolu = yarma ? CizgiRolu.SiyirmaTaban : CizgiRolu.UstyapiAltKotu,
                    AltCizgiRolu = yarma ? CizgiRolu.UstyapiAltKotu : CizgiRolu.SiyirmaTaban,
                    Aciklama = $"TraceBoundary: {malzeme}"
                });

                if (detayliLog)
                    LoggingService.Info($"  {malzeme}: CL sample=({clX:F2},{clOrtaY:F2}), alan={clAlan.Value:F4}");
                return;
            }

            // CL'de boundary bulunamadi — karisik kesit: sol ve sag ayri dene
            double yarmaAlani = 0;
            double dolguAlani = 0;

            double solX = Math.Max(clX - 3.0, minXOrtak + 0.5);
            double sagX = Math.Min(clX + 3.0, maxXOrtak - 0.5);

            // Sol taraf
            if (solX >= minXOrtak && solX <= maxXOrtak)
            {
                double solSiyirmaY = _enKesitAlanService.InterpolateY(siyirmaNkt, solX);
                double solUstyapiY = _enKesitAlanService.InterpolateY(ustyapiAltNkt, solX);
                var solSample = new Point3d(solX, (solSiyirmaY + solUstyapiY) / 2.0, 0);
                double? solAlan = TraceBoundaryAlanAl(solSample);

                if (solAlan.HasValue && solAlan.Value > 0.0001)
                {
                    if (solSiyirmaY > solUstyapiY)
                        yarmaAlani += solAlan.Value;
                    else
                        dolguAlani += solAlan.Value;
                }
            }

            // Sag taraf
            if (sagX >= minXOrtak && sagX <= maxXOrtak)
            {
                double sagSiyirmaY = _enKesitAlanService.InterpolateY(siyirmaNkt, sagX);
                double sagUstyapiY = _enKesitAlanService.InterpolateY(ustyapiAltNkt, sagX);
                var sagSample = new Point3d(sagX, (sagSiyirmaY + sagUstyapiY) / 2.0, 0);
                double? sagAlan = TraceBoundaryAlanAl(sagSample);

                if (sagAlan.HasValue && sagAlan.Value > 0.0001)
                {
                    if (sagSiyirmaY > sagUstyapiY)
                        yarmaAlani += sagAlan.Value;
                    else
                        dolguAlani += sagAlan.Value;
                }
            }

            if (yarmaAlani > 0.0001)
            {
                sonuclar.Add(new AlanHesapSonucu
                {
                    MalzemeAdi = "Yarma",
                    Alan = yarmaAlani,
                    UstCizgiRolu = CizgiRolu.SiyirmaTaban,
                    AltCizgiRolu = CizgiRolu.UstyapiAltKotu,
                    Aciklama = "TraceBoundary: Yarma (karisik kesit)"
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
                    Aciklama = "TraceBoundary: Dolgu (karisik kesit)"
                });
            }

            if (detayliLog)
                LoggingService.Info($"  Yarma/Dolgu karisik: sol={solX:F2} sag={sagX:F2}, Yarma={yarmaAlani:F4}, Dolgu={dolguAlani:F4}");
        }

        // =============== YARDIMCI METODLAR ===============

        /// <summary>
        /// UstyapiAltKotu rolundeki cizgiyi al; yoksa en alt tabaka cizgisini fallback olarak kullan.
        /// </summary>
        private List<Point2d> UstyapiAltNoktalariniAl(KesitGrubu kesit)
        {
            var nkt = RolNoktalariniAl(kesit, CizgiRolu.UstyapiAltKotu);
            if (nkt != null) return nkt;

            var fallbackSirasi = new[]
            {
                CizgiRolu.KirmatasTaban, CizgiRolu.AltTemelTaban, CizgiRolu.PlentmiksTaban,
                CizgiRolu.BitumluTemelTaban, CizgiRolu.BinderTaban, CizgiRolu.AsinmaTaban
            };

            foreach (var fb in fallbackSirasi)
            {
                nkt = RolNoktalariniAl(kesit, fb);
                if (nkt != null) return nkt;
            }

            return null;
        }

        /// <summary>
        /// AutoCAD TraceBoundary API'si ile sample noktasindaki kapali bolgenin alanini hesaplar.
        /// Region olusturur ve .Area ile alan alir. Basarisiz olursa null doner.
        /// </summary>
        private double? TraceBoundaryAlanAl(Point3d nokta)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return null;

                var ed = doc.Editor;
                DBObjectCollection boundaries;

                try
                {
                    boundaries = ed.TraceBoundary(nokta, true);
                }
                catch (System.Exception ex)
                {
                    LoggingService.Warning($"TraceBoundary cagri hatasi ({nokta.X:F2},{nokta.Y:F2}): {ex.Message}");
                    return null;
                }

                if (boundaries == null || boundaries.Count == 0)
                    return null;

                double alan = 0;
                try
                {
                    var curves = new DBObjectCollection();
                    foreach (DBObject obj in boundaries)
                    {
                        if (obj is Curve curve) curves.Add(curve);
                    }

                    if (curves.Count > 0)
                    {
                        var regions = Region.CreateFromCurves(curves);
                        if (regions.Count > 0)
                        {
                            var region = regions[0] as Region;
                            alan = region.Area;
                            region.Dispose();
                            for (int i = 1; i < regions.Count; i++)
                                ((DBObject)regions[i]).Dispose();
                        }
                    }
                }
                catch
                {
                    // Fallback: Polyline.Area
                    foreach (DBObject obj in boundaries)
                    {
                        if (obj is Polyline pl) { alan = pl.Area; break; }
                    }
                }
                finally
                {
                    foreach (DBObject obj in boundaries)
                        obj.Dispose();
                }

                return alan > 0 ? alan : (double?)null;
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning($"TraceBoundaryAlanAl hatasi: {ex.Message}");
                return null;
            }
        }

        // =============== TANILAMA — [3b] TRACEBOUNDARY DETAY ===============

        /// <summary>
        /// Tanilama raporu icin [3b] TRACEBOUNDARY DETAY bolumunu yazar.
        /// Her malzeme cifti icin sample point denemelerini ve TraceBoundary sonuclarini detayli loglar.
        /// </summary>
        internal void TBTanilamaKesitYaz(StringBuilder sb, KesitGrubu kesit)
        {
            sb.AppendLine();
            sb.AppendLine("  [3b] TRACEBOUNDARY DETAY");

            if (!kesit.CL_X.HasValue)
            {
                sb.AppendLine("      CL_X yok — TraceBoundary uygulanamaz");
                return;
            }

            double clX = kesit.CL_X.Value;
            sb.AppendLine($"      CL_X = {clX:F4}");

            // Standart malzeme ciftleri
            var ciftler = new[]
            {
                (ust: CizgiRolu.Zemin, alt: CizgiRolu.SiyirmaTaban, ad: "Siyirma"),
                (ust: CizgiRolu.ProjeKotu, alt: CizgiRolu.AsinmaTaban, ad: "Asinma"),
                (ust: CizgiRolu.AsinmaTaban, alt: CizgiRolu.BinderTaban, ad: "Binder"),
                (ust: CizgiRolu.BinderTaban, alt: CizgiRolu.BitumluTemelTaban, ad: "Bitumlu Temel"),
                (ust: CizgiRolu.BitumluTemelTaban, alt: CizgiRolu.PlentmiksTaban, ad: "Plentmiks"),
                (ust: CizgiRolu.PlentmiksTaban, alt: CizgiRolu.AltTemelTaban, ad: "Alttemel"),
                (ust: CizgiRolu.AltTemelTaban, alt: CizgiRolu.KirmatasTaban, ad: "Kirmatas"),
            };

            foreach (var (ustRol, altRol, ad) in ciftler)
            {
                sb.AppendLine($"      --- {ad} ({ustRol} / {altRol}) ---");

                var ustNkt = RolNoktalariniAl(kesit, ustRol);
                var altNkt = RolNoktalariniAl(kesit, altRol);

                if (ustNkt == null || altNkt == null)
                {
                    sb.AppendLine($"          CIZGI EKSIK: ust({ustRol})={ustNkt?.Count.ToString() ?? "YOK"}, alt({altRol})={altNkt?.Count.ToString() ?? "YOK"}");
                    continue;
                }

                TBSampleDetayYaz(sb, ustNkt, altNkt, clX);
            }

            // Yarma / Dolgu
            sb.AppendLine($"      --- Yarma/Dolgu (SiyirmaTaban / UstyapiAltKotu) ---");
            var siyirmaNkt = RolNoktalariniAl(kesit, CizgiRolu.SiyirmaTaban);
            var ustyapiAltNkt = UstyapiAltNoktalariniAl(kesit);

            if (siyirmaNkt == null || ustyapiAltNkt == null)
            {
                sb.AppendLine($"          CIZGI EKSIK: siyirma={siyirmaNkt?.Count.ToString() ?? "YOK"}, ustyapiAlt={ustyapiAltNkt?.Count.ToString() ?? "YOK"}");
            }
            else
            {
                // CL noktasi
                sb.AppendLine($"          [CL deneme]");
                TBSampleDetayYaz(sb, siyirmaNkt, ustyapiAltNkt, clX);

                // Sol/sag taraf
                double minXOrtak = Math.Max(siyirmaNkt.Min(p => p.X), ustyapiAltNkt.Min(p => p.X));
                double maxXOrtak = Math.Min(siyirmaNkt.Max(p => p.X), ustyapiAltNkt.Max(p => p.X));
                double solX = Math.Max(clX - 3.0, minXOrtak + 0.5);
                double sagX = Math.Min(clX + 3.0, maxXOrtak - 0.5);

                sb.AppendLine($"          [Sol taraf deneme]");
                TBTekNoktaDetayYaz(sb, siyirmaNkt, ustyapiAltNkt, solX);
                sb.AppendLine($"          [Sag taraf deneme]");
                TBTekNoktaDetayYaz(sb, siyirmaNkt, ustyapiAltNkt, sagX);
            }
        }

        /// <summary>
        /// CL_X, CL_X-2, CL_X+2 denemelerini detayli yazar.
        /// </summary>
        private void TBSampleDetayYaz(StringBuilder sb, List<Point2d> ustNkt, List<Point2d> altNkt, double clX)
        {
            double[] denemeXleri = { clX, clX - 2.0, clX + 2.0 };
            string[] etiketler = { "CL", "CL-2", "CL+2" };

            double minXOrtak = Math.Max(ustNkt.Min(p => p.X), altNkt.Min(p => p.X));
            double maxXOrtak = Math.Min(ustNkt.Max(p => p.X), altNkt.Max(p => p.X));
            sb.AppendLine($"          Ortak X araligi: [{minXOrtak:F2}..{maxXOrtak:F2}]");

            for (int i = 0; i < denemeXleri.Length; i++)
            {
                double x = denemeXleri[i];
                string etiket = etiketler[i];
                TBTekNoktaDetayYaz(sb, ustNkt, altNkt, x, etiket);
            }
        }

        /// <summary>
        /// Tek bir X koordinatinda sample point olusturup TraceBoundary detayini yazar.
        /// </summary>
        private void TBTekNoktaDetayYaz(StringBuilder sb, List<Point2d> ustNkt, List<Point2d> altNkt, double x, string etiket = null)
        {
            string lbl = etiket != null ? $"{etiket} (X={x:F2})" : $"X={x:F2}";

            double minXOrtak = Math.Max(ustNkt.Min(p => p.X), altNkt.Min(p => p.X));
            double maxXOrtak = Math.Min(ustNkt.Max(p => p.X), altNkt.Max(p => p.X));

            if (x < minXOrtak || x > maxXOrtak)
            {
                sb.AppendLine($"          {lbl}: X ARALIK DISI [min={minXOrtak:F2}, max={maxXOrtak:F2}]");
                return;
            }

            double ustY = _enKesitAlanService.InterpolateY(ustNkt, x);
            double altY = _enKesitAlanService.InterpolateY(altNkt, x);
            double yFark = Math.Abs(ustY - altY);

            if (yFark < 0.001)
            {
                sb.AppendLine($"          {lbl}: ustY={ustY:F4}, altY={altY:F4}, |fark|={yFark:F6} < 0.001 — ATLIYOR (cizgiler cakisik)");
                return;
            }

            double ortaY = (ustY + altY) / 2.0;
            sb.AppendLine($"          {lbl}: ustY={ustY:F4}, altY={altY:F4}, ortaY={ortaY:F4}, yFark={yFark:F4}");

            // TraceBoundary detayli cagri
            var (alan, detay) = TraceBoundaryDetayliAlanAl(new Point3d(x, ortaY, 0));

            if (alan.HasValue && alan.Value > 0.0001)
                sb.AppendLine($"          → BASARILI: alan={alan.Value:F4} m2 ({detay})");
            else
                sb.AppendLine($"          → BASARISIZ: {detay}");
        }

        /// <summary>
        /// TraceBoundary cagrisinin her adimini tanilayarak (alan, detay) dondurur.
        /// Hesap mantigi TraceBoundaryAlanAl ile ayni, sadece ek tanilama bilgisi toplar.
        /// </summary>
        private (double? alan, string detay) TraceBoundaryDetayliAlanAl(Point3d nokta)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return (null, "MdiActiveDocument=null");

                var ed = doc.Editor;
                DBObjectCollection boundaries;

                try
                {
                    boundaries = ed.TraceBoundary(nokta, true);
                }
                catch (System.Exception ex)
                {
                    return (null, $"TraceBoundary exception: {ex.GetType().Name}: {ex.Message}");
                }

                if (boundaries == null) return (null, "boundaries=null");
                if (boundaries.Count == 0) return (null, "boundaries.Count=0 (acik bolge — kapali sinir yok)");

                // Boundary objelerinin tiplerini say
                int curveCount = 0;
                var tipler = new List<string>();
                foreach (DBObject obj in boundaries)
                {
                    tipler.Add(obj.GetType().Name);
                    if (obj is Curve) curveCount++;
                }
                string tipOzet = string.Join(",", tipler);

                if (curveCount == 0)
                {
                    foreach (DBObject obj in boundaries) obj.Dispose();
                    return (null, $"boundaries={boundaries.Count} ama Curve yok, tipler=[{tipOzet}]");
                }

                double alan = 0;
                string regionDetay;
                try
                {
                    var curves = new DBObjectCollection();
                    foreach (DBObject obj in boundaries)
                        if (obj is Curve c) curves.Add(c);

                    var regions = Region.CreateFromCurves(curves);
                    if (regions.Count > 0)
                    {
                        var region = regions[0] as Region;
                        alan = region.Area;
                        regionDetay = $"Region.Count={regions.Count}, Area={alan:F4}";
                        region.Dispose();
                        for (int i = 1; i < regions.Count; i++)
                            ((DBObject)regions[i]).Dispose();
                    }
                    else
                    {
                        regionDetay = "Region.CreateFromCurves=0 (curve'ler kapali bolge olusturmuyor)";
                    }
                }
                catch (System.Exception ex)
                {
                    regionDetay = $"Region exception: {ex.Message}";
                    foreach (DBObject obj in boundaries)
                    {
                        if (obj is Polyline pl)
                        {
                            alan = pl.Area;
                            regionDetay += $", Polyline.Area fallback={alan:F4}";
                            break;
                        }
                    }
                }
                finally
                {
                    foreach (DBObject obj in boundaries)
                        obj.Dispose();
                }

                string sonuc = $"boundaries={boundaries.Count} [{tipOzet}], curves={curveCount}, {regionDetay}";
                return (alan > 0 ? alan : (double?)null, sonuc);
            }
            catch (System.Exception ex)
            {
                return (null, $"Genel hata: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
