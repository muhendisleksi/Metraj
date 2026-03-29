using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    /// <summary>
    /// Debug gorsellestirme: hesaplanan polygon'lari + ham parcalari DWG'ye cizer.
    /// METRAJDEBUG komutuyla kullanici tiklar, en yakin kesitin polygon'lari cizilir.
    /// </summary>
    public partial class KesitAlanHesapService
    {
        private const string DebugLayerAdi = "METRAJ_DEBUG";

        // Static: instance'lar arasi paylasim (transient DI)
        private static readonly List<DebugPolygonBilgisi> _debugVerileri = new List<DebugPolygonBilgisi>();
        private static readonly Dictionary<string, KesitGrubu> _debugKesitMap = new Dictionary<string, KesitGrubu>();
        private static string _aktifKesitAdi;
        private static double _aktifAnchorX, _aktifAnchorY;
        private static CizgiRolu _debugAktifUstRol, _debugAktifAltRol;

        /// <summary>Hesap sirasinda toplanan polygon verisi.</summary>
        public class DebugPolygonBilgisi
        {
            public string KesitAdi;
            public double AnchorX, AnchorY;
            public string MalzemeAdi;
            public double Alan;
            public List<Point2d> UstKesik, AltKesik, Polygon;
            public CizgiRolu UstRol, AltRol;
        }

        public static List<DebugPolygonBilgisi> DebugVerileriAl() => _debugVerileri;

        private static void DebugVerileriSifirla()
        {
            _debugVerileri.Clear();
            _debugKesitMap.Clear();
            _aktifKesitAdi = null;
        }

        private static void DebugAktifKesitAyarla(KesitGrubu kesit)
        {
            _aktifKesitAdi = kesit.Anchor?.IstasyonMetni ?? "?";
            _aktifAnchorX = kesit.Anchor?.X ?? 0;
            _aktifAnchorY = kesit.Anchor?.Y ?? 0;
            _debugKesitMap[_aktifKesitAdi] = kesit;
        }

        private void DebugPolygonKaydet(string malzemeAdi, double alan,
            List<Point2d> ustKesik, List<Point2d> altKesik, List<Point2d> polygon)
        {
            if (malzemeAdi == null || alan <= 0.0001) return;
            _debugVerileri.Add(new DebugPolygonBilgisi
            {
                KesitAdi = _aktifKesitAdi,
                AnchorX = _aktifAnchorX,
                AnchorY = _aktifAnchorY,
                MalzemeAdi = malzemeAdi,
                Alan = alan,
                UstKesik = new List<Point2d>(ustKesik),
                AltKesik = new List<Point2d>(altKesik),
                Polygon = new List<Point2d>(polygon),
                UstRol = _debugAktifUstRol,
                AltRol = _debugAktifAltRol
            });
        }

        // =============== DWG CIZIM ===============

        public void DebugKatmaniTemizle()
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    var silinecekler = new List<ObjectId>();
                    foreach (ObjectId id in ms)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent != null && ent.Layer == DebugLayerAdi)
                            silinecekler.Add(id);
                    }

                    foreach (var id in silinecekler)
                        ((Entity)tr.GetObject(id, OpenMode.ForWrite)).Erase();

                    tr.Commit();
                    if (silinecekler.Count > 0)
                        LoggingService.Info($"Debug temizlendi: {silinecekler.Count} entity");
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning($"Debug temizleme hatasi: {ex.Message}");
            }
        }

        /// <summary>
        /// Kesitin debug verilerini DWG'ye cizer.
        /// Her malzeme icin: ham parcalar + birlesik cizgiler + kapali polygon + etiket.
        /// </summary>
        public static void DebugKesitCiz(string kesitAdi)
        {
            var kesitVerileri = _debugVerileri.Where(d => d.KesitAdi == kesitAdi).ToList();
            if (kesitVerileri.Count == 0)
            {
                LoggingService.Warning($"Debug: '{kesitAdi}' icin veri yok");
                return;
            }

            _debugKesitMap.TryGetValue(kesitAdi, out var kesitRef);

            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var db = doc.Database;
                    DebugLayerOlustur(db, tr);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    int cizilen = 0;
                    foreach (var veri in kesitVerileri)
                    {
                        // ===== HAM PARCALAR (birlestirme oncesi) =====
                        if (kesitRef != null)
                        {
                            // Ust rol ham parcalari — Cyan (4), kesikli
                            var ustParcalar = kesitRef.Cizgiler
                                .Where(c => c.Rol == veri.UstRol && !c.DikeyVeyaSevMi).ToList();
                            for (int i = 0; i < ustParcalar.Count; i++)
                            {
                                var parca = ustParcalar[i];
                                if (parca.Noktalar.Count < 2) continue;

                                var pl = Point2dListesindenPolyline(parca.Noktalar, false);
                                pl.Layer = DebugLayerAdi;
                                pl.ColorIndex = 4; // Cyan
                                pl.LineWeight = LineWeight.LineWeight035;
                                pl.LinetypeScale = 0.5;
                                ms.AppendEntity(pl);
                                tr.AddNewlyCreatedDBObject(pl, true);

                                // Etiket: parca bilgisi
                                var sonPnt = parca.Noktalar.Last();
                                var lbl = new DBText
                                {
                                    Position = new Point3d(sonPnt.X + 0.1, sonPnt.Y + 0.05, 0),
                                    TextString = $"Ust{i}: {parca.LayerAdi}, {parca.Noktalar.Count}pnt",
                                    Height = 0.08,
                                    Layer = DebugLayerAdi,
                                    ColorIndex = 4
                                };
                                ms.AppendEntity(lbl);
                                tr.AddNewlyCreatedDBObject(lbl, true);
                            }

                            // Alt rol ham parcalari — Magenta (6), kesikli
                            var altParcalar = kesitRef.Cizgiler
                                .Where(c => c.Rol == veri.AltRol && !c.DikeyVeyaSevMi).ToList();
                            for (int i = 0; i < altParcalar.Count; i++)
                            {
                                var parca = altParcalar[i];
                                if (parca.Noktalar.Count < 2) continue;

                                var pl = Point2dListesindenPolyline(parca.Noktalar, false);
                                pl.Layer = DebugLayerAdi;
                                pl.ColorIndex = 6; // Magenta
                                pl.LineWeight = LineWeight.LineWeight035;
                                pl.LinetypeScale = 0.5;
                                ms.AppendEntity(pl);
                                tr.AddNewlyCreatedDBObject(pl, true);

                                var sonPnt = parca.Noktalar.Last();
                                var lbl = new DBText
                                {
                                    Position = new Point3d(sonPnt.X + 0.1, sonPnt.Y - 0.1, 0),
                                    TextString = $"Alt{i}: {parca.LayerAdi}, {parca.Noktalar.Count}pnt",
                                    Height = 0.08,
                                    Layer = DebugLayerAdi,
                                    ColorIndex = 6
                                };
                                ms.AppendEntity(lbl);
                                tr.AddNewlyCreatedDBObject(lbl, true);
                            }
                        }

                        // ===== BIRLESIK CIZGILER (hesapta kullanilan) =====

                        // Birlesik ust — Kirmizi (1)
                        if (veri.UstKesik != null && veri.UstKesik.Count >= 2)
                        {
                            var pl = Point2dListesindenPolyline(veri.UstKesik, false);
                            pl.Layer = DebugLayerAdi;
                            pl.ColorIndex = 1;
                            pl.LineWeight = LineWeight.LineWeight050;
                            ms.AppendEntity(pl);
                            tr.AddNewlyCreatedDBObject(pl, true);
                        }

                        // Birlesik alt — Mavi (5)
                        if (veri.AltKesik != null && veri.AltKesik.Count >= 2)
                        {
                            var pl = Point2dListesindenPolyline(veri.AltKesik, false);
                            pl.Layer = DebugLayerAdi;
                            pl.ColorIndex = 5;
                            pl.LineWeight = LineWeight.LineWeight050;
                            ms.AppendEntity(pl);
                            tr.AddNewlyCreatedDBObject(pl, true);
                        }

                        // ===== KAPALI POLYGON (Shoelace'e giren) =====

                        if (veri.Polygon != null && veri.Polygon.Count >= 3)
                        {
                            var pl = Point2dListesindenPolyline(veri.Polygon, true);
                            pl.Layer = DebugLayerAdi;
                            pl.ColorIndex = 3; // Yesil
                            pl.LineWeight = LineWeight.LineWeight030;
                            ms.AppendEntity(pl);
                            tr.AddNewlyCreatedDBObject(pl, true);

                            // Alan etiketi
                            double cx = veri.Polygon.Average(p => p.X);
                            double cy = veri.Polygon.Average(p => p.Y);
                            var etiket = new DBText
                            {
                                Position = new Point3d(cx, cy, 0),
                                TextString = $"{veri.MalzemeAdi} = {veri.Alan:F2} m\u00B2",
                                Height = 0.12,
                                Layer = DebugLayerAdi,
                                ColorIndex = 7
                            };
                            ms.AppendEntity(etiket);
                            tr.AddNewlyCreatedDBObject(etiket, true);
                        }

                        cizilen++;
                    }

                    tr.Commit();
                    LoggingService.Info($"Debug: {kesitAdi} — {cizilen} malzeme cizildi (ham parcalar + birlesik + polygon)");
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning($"Debug cizim hatasi: {ex.Message}");
            }
        }

        public static string EnYakinKesitBul(double x, double y)
        {
            if (_debugVerileri.Count == 0) return null;

            string enYakin = null;
            double enYakinMesafe = double.MaxValue;

            foreach (var ad in _debugVerileri.Select(d => d.KesitAdi).Distinct())
            {
                var ilk = _debugVerileri.First(d => d.KesitAdi == ad);
                double mesafe = Math.Sqrt(Math.Pow(ilk.AnchorX - x, 2) + Math.Pow(ilk.AnchorY - y, 2));
                if (mesafe < enYakinMesafe)
                {
                    enYakinMesafe = mesafe;
                    enYakin = ad;
                }
            }

            return enYakin;
        }

        private static Polyline Point2dListesindenPolyline(List<Point2d> noktalar, bool kapali)
        {
            var pl = new Polyline();
            for (int i = 0; i < noktalar.Count; i++)
                pl.AddVertexAt(i, noktalar[i], 0, 0, 0);
            if (kapali) pl.Closed = true;
            return pl;
        }

        private static void DebugLayerOlustur(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(DebugLayerAdi)) return;
            lt.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = DebugLayerAdi,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3)
            };
            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }

        // Interface uyumluluk
        public void DebugModuAc() { }
        public void DebugModuKapat() { }
    }
}
