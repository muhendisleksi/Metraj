using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    /// <summary>
    /// Tum entity verilerini TEK bir Transaction'da okuyup belleğe alan servis.
    /// "Oku bir kez, kullan cok kez" prensibi.
    /// </summary>
    public class EntityCacheService
    {
        private readonly IDocumentContext _documentContext;
        private Dictionary<long, EntityCacheVerisi> _cache;

        public EntityCacheService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        /// <summary>Mevcut cache. CacheOlustur cagrilmadan once null.</summary>
        public Dictionary<long, EntityCacheVerisi> Cache => _cache;

        /// <summary>
        /// Tum entity'leri tek Transaction'da okur ve belleğe alir.
        /// ilerlemeCallback: (okunan, toplam) → true dondururse devam, false dondururse iptal.
        /// </summary>
        public Dictionary<long, EntityCacheVerisi> CacheOlustur(
            List<ObjectId> entityIds,
            Func<int, int, bool> ilerlemeCallback = null)
        {
            _cache = new Dictionary<long, EntityCacheVerisi>(entityIds.Count);

            using (var tr = _documentContext.BeginTransaction())
            {
                for (int i = 0; i < entityIds.Count; i++)
                {
                    var entId = entityIds[i];
                    var obj = tr.GetObject(entId, OpenMode.ForRead);
                    if (!(obj is Entity ent)) continue;

                    var cached = EntityOku(ent, entId, tr);
                    if (cached != null)
                        _cache[entId.Handle.Value] = cached;

                    // Her 500 entity'de bir ilerleme bildir
                    if (ilerlemeCallback != null && ((i + 1) % 500 == 0 || i == entityIds.Count - 1))
                    {
                        bool devam = ilerlemeCallback(i + 1, entityIds.Count);
                        if (!devam) break; // iptal
                    }
                }

                tr.Commit();
            }

            LoggingService.Info($"EntityCache: {_cache.Count} entity onbelleklendi ({entityIds.Count} toplam)");
            return _cache;
        }

        /// <summary>Entity tipine gore cache verisi olusturur.</summary>
        private EntityCacheVerisi EntityOku(Entity ent, ObjectId entId, Transaction tr)
        {
            var cached = new EntityCacheVerisi
            {
                Handle = entId.Handle.Value,
                EntityId = entId,
                LayerAdi = ent.Layer,
                RenkIndex = (short)ent.ColorIndex
            };

            // Bounds oku
            try
            {
                var bounds = ent.GeometricExtents;
                cached.MinX = bounds.MinPoint.X;
                cached.MinY = bounds.MinPoint.Y;
                cached.MaxX = bounds.MaxPoint.X;
                cached.MaxY = bounds.MaxPoint.Y;
            }
            catch
            {
                // GeometricExtents alinamazsa bu entity kullanisiz
                return null;
            }

            // Tip belirleme ve veri okuma
            if (ent is DBText dbText)
            {
                cached.Kategori = EntityKategori.Text;
                if (!string.IsNullOrWhiteSpace(dbText.TextString))
                {
                    cached.Textler = new List<(string, double, double)>
                    {
                        (dbText.TextString.Trim(), dbText.Position.X, dbText.Position.Y)
                    };
                }
            }
            else if (ent is MText mText)
            {
                cached.Kategori = EntityKategori.Text;
                if (!string.IsNullOrWhiteSpace(mText.Contents))
                {
                    string temiz = TabloOkumaService.MTextIcerikTemizle(mText.Contents);
                    if (!string.IsNullOrWhiteSpace(temiz))
                    {
                        cached.Textler = new List<(string, double, double)>
                        {
                            (temiz, mText.Location.X, mText.Location.Y)
                        };
                    }
                }
            }
            else if (ent is Table table)
            {
                cached.Kategori = EntityKategori.Tablo;
                cached.Textler = new List<(string, double, double)>();
                TabloHucreleriniOku(table, cached.Textler);
            }
            else if (ent is BlockReference blkRef)
            {
                cached.Kategori = EntityKategori.Blok;
                cached.Textler = new List<(string, double, double)>();
                BlokIciTextleriOku(blkRef, tr, cached.Textler);
            }
            else if (ent is Hatch hatch)
            {
                cached.Kategori = EntityKategori.Cizgi;
                HatchOku(hatch, cached);
            }
            else if (CizgiEntity(ent))
            {
                cached.Kategori = EntityKategori.Cizgi;
                cached.Noktalar = NoktalarOku(ent, tr);
                if (cached.Noktalar == null || cached.Noktalar.Count < 2)
                    return null;
                KapaliVeAlanOku(ent, cached);
            }
            else
            {
                cached.Kategori = EntityKategori.Diger;
            }

            return cached;
        }

        /// <summary>Vertex/nokta bilgisi cikarilabilecek entity tipleri.</summary>
        private static bool CizgiEntity(Entity ent)
        {
            return ent is Polyline
                || ent is Polyline2d
                || ent is Polyline3d
                || ent is Line
                || ent is Face
                || ent is Solid
                || ent is Solid3d
                || ent is Spline
                || ent is Arc
                || ent is Ellipse;
        }

        /// <summary>
        /// Entity'den nokta listesi okur — EnKesitAlanService.PolylineNoktalariniAl ile
        /// ayni mantik, ama AYRI Transaction ACMADAN mevcut tr'yi kullanir.
        /// </summary>
        private List<Point2d> NoktalarOku(Entity ent, Transaction tr)
        {
            var noktalar = new List<Point2d>();

            switch (ent)
            {
                case Polyline pl:
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                        noktalar.Add(pl.GetPoint2dAt(i));
                    break;

                case Polyline2d pl2d:
                    foreach (ObjectId vId in pl2d)
                    {
                        var vertex = tr.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                        if (vertex != null)
                            noktalar.Add(new Point2d(vertex.Position.X, vertex.Position.Y));
                    }
                    break;

                case Polyline3d pl3d:
                    foreach (ObjectId vId in pl3d)
                    {
                        var vertex = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
                        if (vertex != null)
                            noktalar.Add(new Point2d(vertex.Position.X, vertex.Position.Y));
                    }
                    break;

                case Line line:
                    noktalar.Add(new Point2d(line.StartPoint.X, line.StartPoint.Y));
                    noktalar.Add(new Point2d(line.EndPoint.X, line.EndPoint.Y));
                    break;

                case Face face:
                    noktalar.Add(new Point2d(face.GetVertexAt(0).X, face.GetVertexAt(0).Y));
                    noktalar.Add(new Point2d(face.GetVertexAt(1).X, face.GetVertexAt(1).Y));
                    noktalar.Add(new Point2d(face.GetVertexAt(2).X, face.GetVertexAt(2).Y));
                    noktalar.Add(new Point2d(face.GetVertexAt(3).X, face.GetVertexAt(3).Y));
                    break;

                case Solid solid:
                    for (short vi = 0; vi < 4; vi++)
                        noktalar.Add(new Point2d(solid.GetPointAt(vi).X, solid.GetPointAt(vi).Y));
                    // 3. ve 4. vertex ayni olabilir (ucgen), tekrarlari cikar
                    if (noktalar.Count == 4
                        && Math.Abs(noktalar[2].X - noktalar[3].X) < 1e-6
                        && Math.Abs(noktalar[2].Y - noktalar[3].Y) < 1e-6)
                        noktalar.RemoveAt(3);
                    break;

                case Spline spline:
                    int ornekSayisi = Math.Max(spline.NumControlPoints * 3, 20);
                    double startParam = spline.StartParam;
                    double endParam = spline.EndParam;
                    double step = (endParam - startParam) / ornekSayisi;
                    for (int si = 0; si <= ornekSayisi; si++)
                    {
                        var pt = spline.GetPointAtParameter(startParam + si * step);
                        noktalar.Add(new Point2d(pt.X, pt.Y));
                    }
                    break;

                case Arc arc:
                    int arcSamples = Math.Max((int)(arc.TotalAngle / (Math.PI / 18)), 4);
                    double arcStep = (arc.EndParam - arc.StartParam) / arcSamples;
                    for (int ai = 0; ai <= arcSamples; ai++)
                    {
                        var pt = arc.GetPointAtParameter(arc.StartParam + ai * arcStep);
                        noktalar.Add(new Point2d(pt.X, pt.Y));
                    }
                    break;

                case Ellipse ellipse:
                    int ellSamples = 36;
                    double ellStep = (ellipse.EndParam - ellipse.StartParam) / ellSamples;
                    for (int ei = 0; ei <= ellSamples; ei++)
                    {
                        var pt = ellipse.GetPointAtParameter(ellipse.StartParam + ei * ellStep);
                        noktalar.Add(new Point2d(pt.X, pt.Y));
                    }
                    break;
            }

            return noktalar;
        }

        /// <summary>Kapali durumu ve alan degerini okur (mevcut CizgiTanimiOlustur mantigi).</summary>
        private void KapaliVeAlanOku(Entity ent, EntityCacheVerisi cached)
        {
            bool kapali = false;
            double alan = 0;

            try
            {
                switch (ent)
                {
                    case Polyline pl:
                        kapali = pl.Closed;
                        try { alan = pl.Area; } catch { }
                        break;

                    case Polyline2d pl2:
                        kapali = pl2.Closed;
                        try { alan = pl2.Area; } catch { }
                        break;

                    case Polyline3d pl3:
                        kapali = pl3.Closed;
                        try { alan = pl3.Area; } catch { }
                        break;

                    case Face:
                        kapali = true;
                        alan = ShoelaceAlan(cached.Noktalar);
                        break;

                    case Solid:
                        kapali = true;
                        alan = ShoelaceAlan(cached.Noktalar);
                        break;

                    case Spline sp:
                        kapali = sp.Closed;
                        if (kapali) try { alan = sp.Area; } catch { }
                        break;

                    case Ellipse el:
                        kapali = true;
                        try { alan = el.Area; } catch { }
                        break;
                }
            }
            catch { /* Alan okunamadiysa 0 kalir */ }

            // Alan > 0 ise entity kapali kabul et
            if (alan > 0.01 && !kapali) kapali = true;

            cached.KapaliMi = kapali;
            cached.EntityAlani = alan;
        }

        /// <summary>Hatch icin bounds'tan nokta uret + alan oku.</summary>
        private void HatchOku(Hatch hatch, EntityCacheVerisi cached)
        {
            double hatchAlan = 0;
            try { hatchAlan = hatch.Area; } catch { }
            if (hatchAlan <= 0) { cached.Kategori = EntityKategori.Diger; return; }

            cached.Noktalar = new List<Point2d>
            {
                new Point2d(cached.MinX, cached.MinY),
                new Point2d(cached.MaxX, cached.MinY),
                new Point2d(cached.MaxX, cached.MaxY),
                new Point2d(cached.MinX, cached.MaxY)
            };
            cached.KapaliMi = true;
            cached.EntityAlani = hatchAlan;
        }

        /// <summary>
        /// BlockReference icindeki tum text entity'lerini okur.
        /// Mevcut TabloOkumaService.BlokIciTextleriOku mantigi.
        /// </summary>
        private void BlokIciTextleriOku(BlockReference blkRef, Transaction tr, List<(string, double, double)> sonuc)
        {
            try
            {
                // 1. Attribute'lari oku
                foreach (ObjectId attId in blkRef.AttributeCollection)
                {
                    var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (att != null && !string.IsNullOrWhiteSpace(att.TextString))
                        sonuc.Add((att.TextString.Trim(), att.Position.X, att.Position.Y));
                }

                // 2. Blok tanimindaki entity'leri oku
                var btr = tr.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return;

                var xform = blkRef.BlockTransform;

                foreach (ObjectId entId in btr)
                {
                    var ent = tr.GetObject(entId, OpenMode.ForRead);

                    string metin = null;
                    Point3d pos = Point3d.Origin;

                    if (ent is DBText dt)
                    {
                        metin = dt.TextString;
                        pos = dt.Position;
                    }
                    else if (ent is MText mt)
                    {
                        metin = TabloOkumaService.MTextIcerikTemizle(mt.Contents);
                        pos = mt.Location;
                    }
                    else if (ent is AttributeDefinition)
                    {
                        continue; // Zaten AttributeCollection'dan okundu
                    }

                    if (string.IsNullOrWhiteSpace(metin)) continue;

                    var wcsPt = pos.TransformBy(xform);
                    sonuc.Add((metin.Trim(), wcsPt.X, wcsPt.Y));
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning($"Blok ici okuma hatasi ({blkRef.Name}): {ex.Message}");
            }
        }

        /// <summary>AutoCAD Table entity'sinin tum hucrelerini text olarak cikarir.</summary>
        private void TabloHucreleriniOku(Table table, List<(string, double, double)> sonuc)
        {
            try
            {
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    for (int col = 0; col < table.Columns.Count; col++)
                    {
                        string metin = null;
                        try { metin = table.GetTextString(row, col, 0); }
                        catch { continue; }

                        if (string.IsNullOrWhiteSpace(metin)) continue;

                        double x = table.Position.X + col * 5.0;
                        double y = table.Position.Y - row * 1.5;

                        sonuc.Add((metin.Trim(), x, y));
                    }
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning($"Table okuma hatasi: {ex.Message}");
            }
        }

        /// <summary>Shoelace formulu ile alan hesapla.</summary>
        private static double ShoelaceAlan(List<Point2d> noktalar)
        {
            if (noktalar == null || noktalar.Count < 3) return 0;
            double a = 0;
            for (int i = 0; i < noktalar.Count; i++)
            {
                var p1 = noktalar[i];
                var p2 = noktalar[(i + 1) % noktalar.Count];
                a += p1.X * p2.Y - p2.X * p1.Y;
            }
            return Math.Abs(a) / 2.0;
        }
    }
}
