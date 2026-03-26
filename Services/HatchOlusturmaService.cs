using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public class HatchOlusturmaService : IHatchOlusturmaService
    {
        private readonly IEntityService _entityService;
        private readonly IMalzemeHatchAyarService _ayarService;

        public HatchOlusturmaService(IEntityService entityService, IMalzemeHatchAyarService ayarService)
        {
            _entityService = entityService;
            _ayarService = ayarService;
        }

        public (ObjectId hatchId, double alan) HatchOlustur(Point3d nokta, MalzemeHatchAyari ayar)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || ayar == null) return (ObjectId.Null, 0);

            var ed = doc.Editor;
            var db = doc.Database;

            DBObjectCollection boundaries;
            try
            {
                boundaries = ed.TraceBoundary(nokta, true);
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("TraceBoundary hatas\u0131: {Hata}", ex);
                return (ObjectId.Null, 0);
            }

            if (boundaries == null || boundaries.Count == 0)
                return (ObjectId.Null, 0);

            // Alan hesapla (Region yakla\u015F\u0131m\u0131)
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
                foreach (DBObject obj in boundaries)
                {
                    if (obj is Polyline pl) { alan = pl.Area; break; }
                }
            }

            if (alan <= Constants.AlanToleransi)
            {
                DisposeBoundaries(boundaries);
                return (ObjectId.Null, 0);
            }

            // Hatch olu\u015Ftur
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    _entityService.EnsureLayer(tr, db, ayar.LayerAdi, ayar.RenkIndex);

                    var loopIds = new ObjectIdCollection();
                    var tempIds = new List<ObjectId>();

                    foreach (DBObject obj in boundaries)
                    {
                        if (obj is Entity entity)
                        {
                            entity.Layer = ayar.LayerAdi;
                            btr.AppendEntity(entity);
                            tr.AddNewlyCreatedDBObject(entity, true);
                            loopIds.Add(entity.ObjectId);
                            tempIds.Add(entity.ObjectId);
                        }
                    }

                    if (loopIds.Count == 0) { tr.Commit(); return (ObjectId.Null, 0); }

                    var hatch = new Hatch();
                    hatch.SetDatabaseDefaults();
                    hatch.Layer = ayar.LayerAdi;
                    hatch.ColorIndex = ayar.RenkIndex;
                    hatch.Associative = false;

                    // Pattern ayarla
                    if (ayar.HatchPattern == "SOLID" || string.IsNullOrEmpty(ayar.HatchPattern))
                        hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                    else
                        hatch.SetHatchPattern(HatchPatternType.PreDefined, ayar.HatchPattern);

                    // \u015Eeffafl\u0131k
                    byte transparencyByte = (byte)(255 * Math.Max(0, Math.Min(1, ayar.Seffaflik)));
                    hatch.Transparency = new Autodesk.AutoCAD.Colors.Transparency(transparencyByte);

                    btr.AppendEntity(hatch);
                    tr.AddNewlyCreatedDBObject(hatch, true);

                    hatch.AppendLoop(HatchLoopTypes.Outermost, loopIds);
                    hatch.EvaluateHatch(true);

                    foreach (var tempId in tempIds)
                    {
                        var tempEnt = tr.GetObject(tempId, OpenMode.ForWrite) as Entity;
                        tempEnt?.Erase();
                    }

                    tr.Commit();
                    LoggingService.Info("Hatch: {Malzeme}, alan={Alan:F2} m\u00B2", ayar.MalzemeAdi, alan);
                    return (hatch.ObjectId, alan);
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Hatch olu\u015Fturma hatas\u0131: {Malzeme}", ex);
                DisposeBoundaries(boundaries);
                return (ObjectId.Null, 0);
            }
        }

        public ObjectId EtiketYaz(ObjectId hatchId, string kolonHarfi, MalzemeHatchAyari ayar)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || ayar == null) return ObjectId.Null;
            var db = doc.Database;

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var hatch = tr.GetObject(hatchId, OpenMode.ForRead) as Hatch;
                    if (hatch == null || hatch.Bounds == null) { tr.Commit(); return ObjectId.Null; }

                    var bounds = hatch.Bounds.Value;
                    var merkez = new Point3d(
                        (bounds.MinPoint.X + bounds.MaxPoint.X) / 2.0,
                        (bounds.MinPoint.Y + bounds.MaxPoint.Y) / 2.0, 0);

                    _entityService.EnsureLayer(tr, db, ayar.EtiketLayerAdi, ayar.RenkIndex);

                    string etiketMetin = kolonHarfi + ayar.KisaKod;

                    // Etiket boyutunu hatch bounds'a oranla
                    double boundsHeight = bounds.MaxPoint.Y - bounds.MinPoint.Y;
                    double textHeight = Math.Max(0.3, boundsHeight * 0.15);

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    var text = new DBText();
                    text.SetDatabaseDefaults();
                    text.TextString = etiketMetin;
                    text.Height = textHeight;
                    text.Layer = ayar.EtiketLayerAdi;
                    text.ColorIndex = ayar.RenkIndex;
                    text.HorizontalMode = TextHorizontalMode.TextCenter;
                    text.VerticalMode = TextVerticalMode.TextVerticalMid;
                    text.AlignmentPoint = merkez;

                    btr.AppendEntity(text);
                    tr.AddNewlyCreatedDBObject(text, true);

                    tr.Commit();
                    return text.ObjectId;
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Etiket yazma hatas\u0131", ex);
                return ObjectId.Null;
            }
        }

        public void KolonHatchTemizle(string kolonHarfi)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    var etiketLayers = _ayarService.AyarlariYukle().Ayarlar
                        .Select(a => a.EtiketLayerAdi).ToHashSet();

                    foreach (ObjectId id in btr)
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity == null || entity.IsErased) continue;

                        if (entity is DBText text && text.TextString.StartsWith(kolonHarfi) &&
                            etiketLayers.Contains(text.Layer))
                        {
                            entity.UpgradeOpen();
                            entity.Erase();
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Kolon hatch temizleme hatas\u0131", ex);
            }
        }

        public void TumHatchTemizle()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    // T\u00FCm malzeme layer'lar\u0131n\u0131 topla
                    var silinecekLayers = new HashSet<string>();
                    foreach (var ayar in _ayarService.AyarlariYukle().Ayarlar)
                    {
                        silinecekLayers.Add(ayar.LayerAdi);
                        silinecekLayers.Add(ayar.EtiketLayerAdi);
                    }
                    // Eski layer'lar da ekle (geriye uyumluluk)
                    silinecekLayers.Add(Constants.LayerYarmaHatch);
                    silinecekLayers.Add(Constants.LayerDolguHatch);
                    silinecekLayers.Add(Constants.LayerEtiketYarma);
                    silinecekLayers.Add(Constants.LayerEtiketDolgu);

                    foreach (ObjectId id in btr)
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity == null || entity.IsErased) continue;

                        if (silinecekLayers.Contains(entity.Layer))
                        {
                            entity.UpgradeOpen();
                            entity.Erase();
                        }
                    }
                    tr.Commit();
                }
                LoggingService.Info("T\u00FCm hatch ve etiketler temizlendi");
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Hatch temizleme hatas\u0131", ex);
            }
        }

        private void DisposeBoundaries(DBObjectCollection boundaries)
        {
            if (boundaries == null) return;
            foreach (DBObject obj in boundaries)
            {
                try { obj.Dispose(); } catch { }
            }
        }
    }
}
