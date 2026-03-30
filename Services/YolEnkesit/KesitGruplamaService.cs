using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Models.YolEnkesit;
using Metraj.Services.Interfaces;

namespace Metraj.Services.YolEnkesit
{
    public class KesitGruplamaService : IKesitGruplamaService
    {
        private readonly IEnKesitAlanService _enKesitAlanService;
        private readonly EntityCacheService _cacheService;
        private const double MinCizgiUzunlugu = 0.05;

        public KesitGruplamaService(IEnKesitAlanService enKesitAlanService, EntityCacheService cacheService)
        {
            _enKesitAlanService = enKesitAlanService;
            _cacheService = cacheService;
        }

        public List<KesitGrubu> KesitGrupla(List<AnchorNokta> anchorlar, KesitPenceresi pencere, IEnumerable<ObjectId> entityIds)
        {
            var cache = _cacheService.Cache;
            var tumEntityler = entityIds.ToList();
            var kesitler = new List<KesitGrubu>();

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
                    long handle = entId.Handle.Value;
                    if (kesitEntityler.Contains(handle)) continue;
                    if (!cache.TryGetValue(handle, out var cached)) continue;

                    // Bounds kontrolu cache'den
                    if (!PencereKesisimVar(cached, minPt, maxPt)) continue;

                    if (cached.Kategori == EntityKategori.Text || cached.Kategori == EntityKategori.Tablo)
                    {
                        kesit.TextObjeler.Add(entId);
                        kesitEntityler.Add(handle);
                    }
                    else if (cached.Kategori == EntityKategori.Cizgi && cached.Noktalar != null)
                    {
                        var cizgi = new CizgiTanimi
                        {
                            EntityId = cached.EntityId,
                            LayerAdi = cached.LayerAdi,
                            RenkIndex = cached.RenkIndex,
                            Noktalar = new List<Point2d>(cached.Noktalar), // KOPYA
                            KapaliMi = cached.KapaliMi,
                            EntityAlani = cached.EntityAlani
                        };

                        // Guvenli clipping: extrapolasyon YAPMA, sadece tasan kismi kes
                        cizgi.Noktalar = GuvenliClip(cizgi.Noktalar, minPt.X, maxPt.X);

                        if (cizgi.Noktalar.Count >= 2)
                        {
                            cizgi.OrtalamaY = cizgi.Noktalar.Average(p => p.Y);
                            if (CizgiGecerliMi(cizgi))
                            {
                                kesit.Cizgiler.Add(cizgi);
                                kesitEntityler.Add(handle);
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
            int tabloTextToplam = 0;
            foreach (var kesit in kesitler)
            {
                int onceki = kesit.TextObjeler.Count;
                var mevcutTextler = new HashSet<long>(kesit.TextObjeler.Select(id => id.Handle.Value));
                var anchor = kesit.Anchor;

                Point3d tabloMinPt, tabloMaxPt;
                if (anchor.CL_Dogrulandi && pencere.OtomatikTespit)
                {
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
                    long handle = entId.Handle.Value;
                    if (mevcutTextler.Contains(handle)) continue;
                    if (!cache.TryGetValue(handle, out var cached)) continue;

                    // Sadece text-icerikli entity'leri kabul et
                    if (cached.Kategori != EntityKategori.Text
                        && cached.Kategori != EntityKategori.Tablo
                        && cached.Kategori != EntityKategori.Blok) continue;

                    if (PencereKesisimVar(cached, tabloMinPt, tabloMaxPt))
                    {
                        kesit.TextObjeler.Add(entId);
                        mevcutTextler.Add(handle);
                    }
                }

                int eklenen = kesit.TextObjeler.Count - onceki;
                if (eklenen > 0) tabloTextToplam += eklenen;
            }

            if (tabloTextToplam > 0)
                LoggingService.Info($"Tablo text taramasi: {tabloTextToplam} ek text bulundu ({kesitler.Count} kesit)");
            else
                LoggingService.Warning("Tablo text taramasi: Genisletilmis pencerede ek text bulunamadi");

            // CL bulunamayan kesitleri logla
            int clEksik = kesitler.Count(k => k.CLEksik);
            if (clEksik > 0)
                LoggingService.Warning($"CL eksik: {clEksik}/{kesitler.Count} kesitte CL cizgisi bulunamadi");

            LoggingService.Info($"Kesit gruplama: {kesitler.Count} kesit, toplam {kesitler.Sum(k => k.Cizgiler.Count)} cizgi");
            return kesitler;
        }

        private bool PencereKesisimVar(EntityCacheVerisi cached, Point3d minPt, Point3d maxPt)
        {
            return cached.MaxX >= minPt.X && cached.MinX <= maxPt.X &&
                   cached.MaxY >= minPt.Y && cached.MinY <= maxPt.Y;
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
                enIyiCL.Rol = CizgiRolu.Diger;
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
