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

                    if (ayar.HatchPattern == "SOLID" || string.IsNullOrEmpty(ayar.HatchPattern))
                        hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                    else
                        hatch.SetHatchPattern(HatchPatternType.PreDefined, ayar.HatchPattern);

                    byte alpha = (byte)(254 * (1.0 - Math.Max(0, Math.Min(1, ayar.Seffaflik))));
                    hatch.Transparency = new Autodesk.AutoCAD.Colors.Transparency(alpha);

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

        public (ObjectId hatchId, double alan) NesnedenHatchOlustur(ObjectId nesneId, MalzemeHatchAyari ayar)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || ayar == null || nesneId.IsNull) return (ObjectId.Null, 0);
            var db = doc.Database;

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var entity = tr.GetObject(nesneId, OpenMode.ForRead) as Entity;
                    if (entity == null) { tr.Commit(); return (ObjectId.Null, 0); }

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    _entityService.EnsureLayer(tr, db, ayar.LayerAdi, ayar.RenkIndex);

                    // Alan hesapla
                    double alan = 0;
                    if (entity is Polyline pl && pl.Closed) alan = pl.Area;
                    else if (entity is Hatch existH) { try { alan = existH.Area; } catch { } }
                    else if (entity is Circle ci) alan = Math.PI * ci.Radius * ci.Radius;

                    var hatch = new Hatch();
                    hatch.SetDatabaseDefaults();
                    hatch.Layer = ayar.LayerAdi;
                    hatch.ColorIndex = ayar.RenkIndex;
                    hatch.Associative = false;
                    hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                    byte a = (byte)(254 * (1.0 - Math.Max(0, Math.Min(1, ayar.Seffaflik))));
                    hatch.Transparency = new Autodesk.AutoCAD.Colors.Transparency(a);

                    btr.AppendEntity(hatch);
                    tr.AddNewlyCreatedDBObject(hatch, true);

                    // Nesneyi boundary loop olarak ekle
                    var loopIds = new ObjectIdCollection();
                    loopIds.Add(nesneId);
                    hatch.AppendLoop(HatchLoopTypes.Outermost, loopIds);
                    hatch.EvaluateHatch(true);

                    if (alan <= 0) { try { alan = hatch.Area; } catch { } }

                    tr.Commit();
                    return (hatch.ObjectId, alan);
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Nesneden hatch olu\u015Fturma hatas\u0131", ex);
                return (ObjectId.Null, 0);
            }
        }

        public ObjectId EtiketYaz(ObjectId hatchId, string kolonHarfi, MalzemeHatchAyari ayar, Point3d? icNokta = null)
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

                    _entityService.EnsureLayer(tr, db, ayar.EtiketLayerAdi, ayar.RenkIndex);
                    string etiketMetin = kolonHarfi + ayar.KisaKod;

                    // Pozisyon: icNokta tiklama/secim noktasi, hatch icinde oldugu garanti
                    var bounds = hatch.Bounds.Value;
                    Point3d etiketPoz = icNokta ?? new Point3d(
                        (bounds.MinPoint.X + bounds.MaxPoint.X) / 2.0,
                        (bounds.MinPoint.Y + bounds.MaxPoint.Y) / 2.0, 0);

                    // Boyut: kisa kenar bazli, Constants.EtiketYuksekligi ile sinirli
                    double boundsH = bounds.MaxPoint.Y - bounds.MinPoint.Y;
                    double boundsW = bounds.MaxPoint.X - bounds.MinPoint.X;
                    double textHeight = Math.Min(boundsH, boundsW) * 0.08;
                    textHeight = Math.Max(0.08, Math.Min(textHeight, Constants.EtiketYuksekligi));

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
                    text.AlignmentPoint = etiketPoz;

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

        public (ObjectId hatchId, double toplamAlan) CokluHatchOlustur(List<Point3d> noktalar, MalzemeHatchAyari ayar, List<ObjectId> nesneEntityIds = null)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || ayar == null)
                return (ObjectId.Null, 0);
            bool tiklamaVar = noktalar != null && noktalar.Count > 0;
            bool nesneVar = nesneEntityIds != null && nesneEntityIds.Count > 0;
            if (!tiklamaVar && !nesneVar)
                return (ObjectId.Null, 0);

            var ed = doc.Editor;
            var db = doc.Database;

            var tumBoundaries = new List<DBObjectCollection>();
            double toplamAlan = 0;

            if (tiklamaVar)
            {
                foreach (var nokta in noktalar)
                {
                    try
                    {
                        var boundaries = ed.TraceBoundary(nokta, true);
                        if (boundaries != null && boundaries.Count > 0)
                        {
                            double alan = 0;
                            var curves = new DBObjectCollection();
                            foreach (DBObject obj in boundaries)
                                if (obj is Curve c) curves.Add(c);

                            if (curves.Count > 0)
                            {
                                try
                                {
                                    var regions = Region.CreateFromCurves(curves);
                                    if (regions.Count > 0) { alan = ((Region)regions[0]).Area; for (int i = 0; i < regions.Count; i++) ((DBObject)regions[i]).Dispose(); }
                                }
                                catch { foreach (DBObject o in boundaries) if (o is Polyline p) { alan = p.Area; break; } }
                            }

                            toplamAlan += alan;
                            tumBoundaries.Add(boundaries);
                        }
                    }
                    catch { }
                }
            }

            if (tumBoundaries.Count == 0 && !nesneVar) return (ObjectId.Null, 0);

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    _entityService.EnsureLayer(tr, db, ayar.LayerAdi, ayar.RenkIndex);

                    var hatch = new Hatch();
                    hatch.SetDatabaseDefaults();
                    hatch.Layer = ayar.LayerAdi;
                    hatch.ColorIndex = ayar.RenkIndex;
                    hatch.Associative = false;
                    hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                    byte alpha2 = (byte)(254 * (1.0 - Math.Max(0, Math.Min(1, ayar.Seffaflik))));
                    hatch.Transparency = new Autodesk.AutoCAD.Colors.Transparency(alpha2);

                    btr.AppendEntity(hatch);
                    tr.AddNewlyCreatedDBObject(hatch, true);

                    // Tiklama boundary loop'lari
                    foreach (var boundaries in tumBoundaries)
                    {
                        var loopIds = new ObjectIdCollection();
                        var tempIds = new List<ObjectId>();

                        foreach (DBObject obj in boundaries)
                        {
                            if (obj is Entity ent)
                            {
                                ent.Layer = ayar.LayerAdi;
                                btr.AppendEntity(ent);
                                tr.AddNewlyCreatedDBObject(ent, true);
                                loopIds.Add(ent.ObjectId);
                                tempIds.Add(ent.ObjectId);
                            }
                        }

                        if (loopIds.Count > 0)
                            hatch.AppendLoop(HatchLoopTypes.Outermost, loopIds);

                        foreach (var tid in tempIds)
                        {
                            var te = tr.GetObject(tid, OpenMode.ForWrite) as Entity;
                            te?.Erase();
                        }
                    }

                    // Nesne entity loop'lari
                    if (nesneVar)
                    {
                        foreach (var nesneId in nesneEntityIds)
                        {
                            if (nesneId.IsNull) continue;
                            try
                            {
                                var entity = tr.GetObject(nesneId, OpenMode.ForRead) as Entity;
                                if (entity == null) continue;

                                var loopIds = new ObjectIdCollection();
                                loopIds.Add(nesneId);
                                hatch.AppendLoop(HatchLoopTypes.Outermost, loopIds);
                            }
                            catch { }
                        }
                    }

                    hatch.EvaluateHatch(true);
                    tr.Commit();
                    return (hatch.ObjectId, toplamAlan);
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Coklu hatch olu\u015Fturma hatas\u0131", ex);
                foreach (var b in tumBoundaries) DisposeBoundaries(b);
                return (ObjectId.Null, 0);
            }
        }

        public void HatchSil(ObjectId hatchId)
        {
            if (hatchId.IsNull) return;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(hatchId, OpenMode.ForWrite) as Entity;
                    ent?.Erase();
                    tr.Commit();
                }
            }
            catch { }
        }

        public void MalzemeHatchSil(string layerAdi, string etiketLayerAdi, List<double[]> tiklamaNoktalari)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || tiklamaNoktalari == null || tiklamaNoktalari.Count == 0) return;
            var db = doc.Database;

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (ObjectId id in btr)
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity == null || entity.IsErased) continue;

                        // Hatch sil: tiklama noktasi hatch bounds icindeyse
                        if (entity.Layer == layerAdi && entity is Hatch hatch && hatch.Bounds.HasValue)
                        {
                            var b = hatch.Bounds.Value;
                            bool iceriyor = tiklamaNoktalari.Any(n => n.Length >= 2 &&
                                n[0] >= b.MinPoint.X - 0.1 && n[0] <= b.MaxPoint.X + 0.1 &&
                                n[1] >= b.MinPoint.Y - 0.1 && n[1] <= b.MaxPoint.Y + 0.1);
                            if (iceriyor) { entity.UpgradeOpen(); entity.Erase(); }
                        }
                        // Etiket sil: ayni bounds icindeki DBText
                        else if (entity.Layer == etiketLayerAdi && entity is DBText text)
                        {
                            var tp = text.AlignmentPoint;
                            bool yakin = tiklamaNoktalari.Any(n => n.Length >= 2 &&
                                System.Math.Abs(tp.X - n[0]) < 50 && System.Math.Abs(tp.Y - n[1]) < 50);
                            if (yakin) { entity.UpgradeOpen(); entity.Erase(); }
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Error("Malzeme hatch silme hatasi", ex);
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
