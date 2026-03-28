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
        private const double MinCizgiUzunlugu = 0.1;

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
                            if (!PencereIcindeMi(bounds, minPt, maxPt)) continue;

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
                        kesitler.Add(kesit);
                }

                tr.Commit();
            }

            LoggingService.Info($"Kesit gruplama: {kesitler.Count} kesit, toplam {kesitler.Sum(k => k.Cizgiler.Count)} çizgi");
            return kesitler;
        }

        private bool PencereIcindeMi(Extents3d bounds, Point3d minPt, Point3d maxPt)
        {
            return bounds.MinPoint.X >= minPt.X - 0.5 && bounds.MaxPoint.X <= maxPt.X + 0.5 &&
                   bounds.MinPoint.Y >= minPt.Y - 0.5 && bounds.MaxPoint.Y <= maxPt.Y + 0.5;
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
            return (maxX - minX) >= MinCizgiUzunlugu;
        }
    }
}
