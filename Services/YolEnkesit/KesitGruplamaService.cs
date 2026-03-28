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
            var atanmisEntityler = new HashSet<long>();
            var kesitler = new List<KesitGrubu>();

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var anchor in anchorlar)
                {
                    var kesit = new KesitGrubu { Anchor = anchor };
                    var minPt = new Point3d(anchor.X - pencere.OffsetSolX, anchor.Y - pencere.OffsetAltY, 0);
                    var maxPt = new Point3d(anchor.X + pencere.OffsetSagX, anchor.Y + pencere.OffsetUstY, 0);

                    foreach (var entId in tumEntityler)
                    {
                        if (atanmisEntityler.Contains(entId.Handle.Value)) continue;

                        var obj = tr.GetObject(entId, OpenMode.ForRead);
                        if (obj is Entity ent)
                        {
                            Extents3d bounds;
                            try { bounds = ent.GeometricExtents; }
                            catch { continue; }

                            if (!PencereKesisimVar(bounds, minPt, maxPt)) continue;

                            if (ent is Polyline || ent is Polyline2d || ent is Polyline3d || ent is Line)
                            {
                                var cizgi = CizgiTanimiOlustur(ent, entId, tr);
                                if (cizgi != null && CizgiGecerliMi(cizgi))
                                {
                                    kesit.Cizgiler.Add(cizgi);
                                    atanmisEntityler.Add(entId.Handle.Value);
                                }
                            }
                            else if (ent is DBText || ent is MText)
                            {
                                kesit.TextObjeler.Add(entId);
                                atanmisEntityler.Add(entId.Handle.Value);
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

        private CizgiTanimi CizgiTanimiOlustur(Entity ent, ObjectId entId, Transaction tr)
        {
            var noktalar = _enKesitAlanService.PolylineNoktalariniAl(entId);
            if (noktalar == null || noktalar.Count < 2) return null;

            return new CizgiTanimi
            {
                EntityId = entId,
                LayerAdi = ent.Layer,
                RenkIndex = (short)ent.ColorIndex,
                Noktalar = noktalar,
                OrtalamaY = noktalar.Average(p => p.Y)
            };
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
