using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.YolEnkesit;
using Metraj.Services.Interfaces;

namespace Metraj.Services.YolEnkesit
{
    public class KesitGruplamaService : IKesitGruplamaService
    {
        private readonly IDocumentContext _documentContext;
        private readonly IEnKesitAlanService _enKesitAlanService;
        private const double MinCizgiUzunlugu = 0.05;

        public KesitGruplamaService(IDocumentContext documentContext, IEnKesitAlanService enKesitAlanService)
        {
            _documentContext = documentContext;
            _enKesitAlanService = enKesitAlanService;
        }

        public List<KesitGrubu> KesitGrupla(List<AnchorNokta> anchorlar, KesitPenceresi pencere, IEnumerable<ObjectId> entityIds)
        {
            var tumEntityler = entityIds.ToList();
            // NOT: atanmisEntityler KALDIRILDI — ayni entity (orn. zemin cizgisi)
            // birden fazla kesitin penceresine dusebilir ve her kesit onu gormeli.
            var kesitler = new List<KesitGrubu>();

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var anchor in anchorlar)
                {
                    var kesit = new KesitGrubu { Anchor = anchor };

                    Point3d minPt, maxPt;
                    if (anchor.CL_Dogrulandi && pencere.OtomatikTespit)
                    {
                        double yMargin = 2.0;
                        minPt = new Point3d(anchor.CL_X - pencere.PlatformYariGenislik, anchor.CL_MinY - yMargin, 0);
                        maxPt = new Point3d(anchor.CL_X + pencere.PlatformYariGenislik, anchor.CL_MaxY + yMargin, 0);
                        kesit.CL_X = anchor.CL_X;
                    }
                    else
                    {
                        minPt = new Point3d(anchor.X - pencere.OffsetSolX, anchor.Y - pencere.OffsetAltY, 0);
                        maxPt = new Point3d(anchor.X + pencere.OffsetSagX, anchor.Y + pencere.OffsetUstY, 0);
                    }

                    // Ayni entity'nin tekrarini engelle (ayni kesitte)
                    var kesitEntityler = new HashSet<long>();

                    foreach (var entId in tumEntityler)
                    {
                        if (kesitEntityler.Contains(entId.Handle.Value)) continue;

                        var obj = tr.GetObject(entId, OpenMode.ForRead);
                        if (obj is Entity ent)
                        {
                            Extents3d bounds;
                            try { bounds = ent.GeometricExtents; }
                            catch { continue; }

                            if (!PencereKesisimVar(bounds, minPt, maxPt)) continue;

                            if (ent is DBText || ent is MText || ent is Table)
                            {
                                kesit.TextObjeler.Add(entId);
                                kesitEntityler.Add(entId.Handle.Value);
                            }
                            else if (CizgiOlabilecekEntity(ent))
                            {
                                var cizgi = CizgiTanimiOlustur(ent, entId, tr);
                                if (cizgi != null)
                                {
                                    // Guvenli clipping: extrapolasyon YAPMA, sadece tasan kismi kes
                                    cizgi.Noktalar = GuvenliClip(cizgi.Noktalar, minPt.X, maxPt.X);

                                    if (cizgi.Noktalar.Count >= 2)
                                    {
                                        cizgi.OrtalamaY = cizgi.Noktalar.Average(p => p.Y);
                                        if (CizgiGecerliMi(cizgi))
                                        {
                                            kesit.Cizgiler.Add(cizgi);
                                            kesitEntityler.Add(entId.Handle.Value);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (kesit.Cizgiler.Count > 0)
                    {
                        // CL (dikey eksen) cizgisi tespiti
                        CL_Tespit(kesit);
                        kesitler.Add(kesit);
                        LogLayerDagilimi(kesit);
                    }
                }

                // Ikinci pass: Malzeme tablosu text'leri genisletilmis pencerede ara
                // Tablo, kesit cizim alaninin SAG tarafinda bulunur
                int tabloTextToplam = 0;
                foreach (var kesit in kesitler)
                {
                    int onceki = kesit.TextObjeler.Count;
                    var mevcutTextler = new HashSet<long>(kesit.TextObjeler.Select(id => id.Handle.Value));
                    var anchor = kesit.Anchor;

                    Point3d tabloMinPt, tabloMaxPt;
                    if (anchor.CL_Dogrulandi && pencere.OtomatikTespit)
                    {
                        // Tablo: kesitin sag disinda — genisletilmis arama alani
                        double sagKenar = anchor.CL_X + pencere.PlatformYariGenislik;
                        tabloMinPt = new Point3d(sagKenar - 2, anchor.CL_MinY - 10, 0);
                        tabloMaxPt = new Point3d(sagKenar + 25, anchor.CL_MaxY + 10, 0);
                    }
                    else
                    {
                        double sagKenar = anchor.X + pencere.OffsetSagX;
                        tabloMinPt = new Point3d(sagKenar - 2, anchor.Y - pencere.OffsetAltY - 10, 0);
                        tabloMaxPt = new Point3d(sagKenar + 25, anchor.Y + pencere.OffsetUstY + 10, 0);
                    }

                    foreach (var entId in tumEntityler)
                    {
                        if (mevcutTextler.Contains(entId.Handle.Value)) continue;

                        var obj = tr.GetObject(entId, OpenMode.ForRead);
                        if (!(obj is DBText) && !(obj is MText) && !(obj is Table) && !(obj is BlockReference)) continue;

                        var ent = (Entity)obj;
                        Extents3d bounds;
                        try { bounds = ent.GeometricExtents; }
                        catch { continue; }

                        if (PencereKesisimVar(bounds, tabloMinPt, tabloMaxPt))
                        {
                            kesit.TextObjeler.Add(entId);
                            mevcutTextler.Add(entId.Handle.Value);
                        }
                    }

                    int eklenen = kesit.TextObjeler.Count - onceki;
                    if (eklenen > 0) tabloTextToplam += eklenen;
                }

                if (tabloTextToplam > 0)
                    LoggingService.Info($"Tablo text taramasi: {tabloTextToplam} ek text bulundu ({kesitler.Count} kesit)");
                else
                    LoggingService.Warning("Tablo text taramasi: Genisletilmis pencerede ek text bulunamadi");

                tr.Commit();
            }

            // CL bulunamayan kesitleri logla
            int clEksik = kesitler.Count(k => k.CLEksik);
            if (clEksik > 0)
                LoggingService.Warning($"CL eksik: {clEksik}/{kesitler.Count} kesitte CL cizgisi bulunamadi");

            LoggingService.Info($"Kesit gruplama: {kesitler.Count} kesit, toplam {kesitler.Sum(k => k.Cizgiler.Count)} cizgi");
            return kesitler;
        }

        private bool PencereKesisimVar(Extents3d bounds, Point3d minPt, Point3d maxPt)
        {
            return bounds.MaxPoint.X >= minPt.X && bounds.MinPoint.X <= maxPt.X &&
                   bounds.MaxPoint.Y >= minPt.Y && bounds.MinPoint.Y <= maxPt.Y;
        }

        /// <summary>
        /// Vertex/nokta bilgisi cikarilabilecek entity tiplerini kabul et.
        /// PolylineNoktalariniAl desteklemedigi tipler icin bos liste doner,
        /// CizgiGecerliMi filtresiyle elenirler.
        /// </summary>
        private bool CizgiOlabilecekEntity(Entity ent)
        {
            return ent is Polyline
                || ent is Polyline2d
                || ent is Polyline3d
                || ent is Line
                || ent is Face          // 3dFace — ihale dosyalarinda ustyapi tabakalari
                || ent is Solid         // 2D Solid (trace)
                || ent is Solid3d       // 3D Solid
                || ent is Spline
                || ent is Arc
                || ent is Ellipse
                || ent is Hatch;        // Hatch — kapali bolge, direkt alan okunabilir
        }

        private CizgiTanimi CizgiTanimiOlustur(Entity ent, ObjectId entId, Transaction tr)
        {
            // Hatch ozel durum: noktasi olmayabilir ama alani var
            if (ent is Hatch hatch)
            {
                double hatchAlan = 0;
                try { hatchAlan = hatch.Area; } catch { return null; }
                if (hatchAlan <= 0) return null;

                Extents3d hb;
                try { hb = hatch.GeometricExtents; } catch { return null; }

                // Hatch icin boundary noktalarini kullan (goruntuleme icin)
                var noktalarH = new List<Point2d>
                {
                    new Point2d(hb.MinPoint.X, hb.MinPoint.Y),
                    new Point2d(hb.MaxPoint.X, hb.MinPoint.Y),
                    new Point2d(hb.MaxPoint.X, hb.MaxPoint.Y),
                    new Point2d(hb.MinPoint.X, hb.MaxPoint.Y)
                };

                return new CizgiTanimi
                {
                    EntityId = entId,
                    LayerAdi = ent.Layer,
                    RenkIndex = (short)ent.ColorIndex,
                    Noktalar = noktalarH,
                    OrtalamaY = (hb.MinPoint.Y + hb.MaxPoint.Y) / 2,
                    KapaliMi = true,
                    EntityAlani = hatchAlan
                };
            }

            var noktalar = _enKesitAlanService.PolylineNoktalariniAl(entId);
            if (noktalar == null || noktalar.Count < 2) return null;

            // Kapali entity kontrolu ve alan okuma
            bool kapali = false;
            double alan = 0;
            try
            {
                if (ent is Polyline pl)
                {
                    kapali = pl.Closed;
                    // Kapali olsun olmasin .Area'yi dene (4 noktali kapali parcalar icin)
                    try { alan = pl.Area; } catch { }
                    // Polyline.Area acik cizgilerde 0 veya hata verir — sorun degil
                }
                else if (ent is Polyline2d pl2)
                {
                    kapali = pl2.Closed;
                    try { alan = pl2.Area; } catch { }
                }
                else if (ent is Polyline3d pl3)
                {
                    kapali = pl3.Closed;
                    try { alan = pl3.Area; } catch { }
                }
                else if (ent is Face face)
                {
                    // 3dFace: 3-4 vertex ile tanimlanan yuzey — her zaman alan tasir
                    kapali = true;
                    // Face.Area yok — Shoelace ile hesapla
                    if (noktalar.Count >= 3)
                    {
                        double a = 0;
                        for (int i = 0; i < noktalar.Count; i++)
                        {
                            var p1 = noktalar[i];
                            var p2 = noktalar[(i + 1) % noktalar.Count];
                            a += p1.X * p2.Y - p2.X * p1.Y;
                        }
                        alan = Math.Abs(a) / 2.0;
                    }
                }
                else if (ent is Solid solid)
                {
                    // 2D Solid: 3-4 vertex
                    kapali = true;
                    if (noktalar.Count >= 3)
                    {
                        double a = 0;
                        for (int i = 0; i < noktalar.Count; i++)
                        {
                            var p1 = noktalar[i];
                            var p2 = noktalar[(i + 1) % noktalar.Count];
                            a += p1.X * p2.Y - p2.X * p1.Y;
                        }
                        alan = Math.Abs(a) / 2.0;
                    }
                }
                else if (ent is Spline sp)
                {
                    kapali = sp.Closed;
                    if (kapali) try { alan = sp.Area; } catch { }
                }
                else if (ent is Ellipse el)
                {
                    kapali = true;
                    try { alan = el.Area; } catch { }
                }
            }
            catch { /* Alan okunamadiysa 0 kalir */ }

            // Alan > 0 ise entity kapali kabul et (bazi Polyline'lar Closed=false ama Area > 0)
            if (alan > 0.01 && !kapali) kapali = true;

            return new CizgiTanimi
            {
                EntityId = entId,
                LayerAdi = ent.Layer,
                RenkIndex = (short)ent.ColorIndex,
                Noktalar = noktalar,
                OrtalamaY = noktalar.Average(p => p.Y),
                KapaliMi = kapali,
                EntityAlani = alan
            };
        }

        /// <summary>
        /// Cizgi noktalarini pencere X sinirlarinda kirpar.
        /// EXTRAPOLASYON YAPMAZ — cizgi pencereden kisaysa dokunmaz.
        /// Sadece cizginin pencereden tasan kisimlarini keser ve
        /// sinir noktalarinda interpolasyon yapar.
        /// </summary>
        private List<Point2d> GuvenliClip(List<Point2d> noktalar, double pencereMinX, double pencereMaxX)
        {
            if (noktalar == null || noktalar.Count < 2) return noktalar;

            double entMinX = noktalar.Min(p => p.X);
            double entMaxX = noktalar.Max(p => p.X);

            // Gercek kesisim var mi?
            double overlapMin = Math.Max(entMinX, pencereMinX);
            double overlapMax = Math.Min(entMaxX, pencereMaxX);
            if (overlapMax - overlapMin < MinCizgiUzunlugu)
                return new List<Point2d>(); // kesisim yok

            // Cizgi pencere icinde mi? Dokunma.
            if (entMinX >= pencereMinX && entMaxX <= pencereMaxX)
                return noktalar;

            // Cizgi pencereden tasiyor — sadece tasan kismi kes
            // Clip sinirlari: cizginin kendi araligi ile pencerenin kesisimi
            double clipMin = Math.Max(entMinX, pencereMinX);
            double clipMax = Math.Min(entMaxX, pencereMaxX);

            return _enKesitAlanService.ClipToXRange(noktalar, clipMin, clipMax);
        }

        private bool CizgiGecerliMi(CizgiTanimi cizgi)
        {
            if (cizgi.Noktalar.Count < 2) return false;
            double minX = cizgi.Noktalar.Min(p => p.X);
            double maxX = cizgi.Noktalar.Max(p => p.X);
            double minY = cizgi.Noktalar.Min(p => p.Y);
            double maxY = cizgi.Noktalar.Max(p => p.Y);
            return (maxX - minX) >= MinCizgiUzunlugu || (maxY - minY) >= MinCizgiUzunlugu;
        }

        /// <summary>
        /// Dikey cizgiyi bul: X araligi cok dar, Y araligi genis.
        /// Bu cizgi CL (center line) eksenidir.
        /// </summary>
        private void CL_Tespit(KesitGrubu kesit)
        {
            CizgiTanimi enIyiCL = null;
            double enBuyukYAraligi = 0;

            foreach (var cizgi in kesit.Cizgiler)
            {
                if (cizgi.Noktalar.Count < 2) continue;

                double xMin = cizgi.Noktalar.Min(p => p.X);
                double xMax = cizgi.Noktalar.Max(p => p.X);
                double yMin = cizgi.Noktalar.Min(p => p.Y);
                double yMax = cizgi.Noktalar.Max(p => p.Y);
                double xAraligi = xMax - xMin;
                double yAraligi = yMax - yMin;

                // Dikey cizgi: X araligi < 0.5 birim VE Y araligi > 3 birim VE Y/X orani > 10
                if (xAraligi < 0.5 && yAraligi > 3 && (xAraligi < 0.01 || yAraligi / xAraligi > 10))
                {
                    if (yAraligi > enBuyukYAraligi)
                    {
                        enBuyukYAraligi = yAraligi;
                        enIyiCL = cizgi;
                    }
                }
            }

            if (enIyiCL != null)
            {
                enIyiCL.Rol = CizgiRolu.EksenCizgisi;
                enIyiCL.OtomatikAtanmis = true;
                kesit.CL_X = enIyiCL.Noktalar.Average(p => p.X);
            }
        }

        private void LogLayerDagilimi(KesitGrubu kesit)
        {
            var dagilim = kesit.Cizgiler.GroupBy(c => c.LayerAdi)
                .Select(g => $"{g.Key}:{g.Count()}")
                .ToList();
            string clBilgi = kesit.CL_X.HasValue ? $"CL={kesit.CL_X:F1}" : "CL=YOK";
            LoggingService.Info($"Kesit {kesit.Anchor?.IstasyonMetni}: {kesit.Cizgiler.Count} cizgi, {clBilgi} — {string.Join(", ", dagilim)}");
        }
    }
}
