using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.IhaleKontrol;
using Metraj.Services.IhaleKontrol.Interfaces;

namespace Metraj.Services.IhaleKontrol
{
    public class GeometrikAlanService : IGeometrikAlanService
    {
        private readonly IDocumentContext _documentContext;
        private const double SampleAraligi = 0.5; // metre

        public GeometrikAlanService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public List<GeometrikKesitVerisi> TumKesitleriHesapla(
            List<KesitBolge> bolgeler, ReferansKesitAyarlari referans)
        {
            var sonuclar = new List<GeometrikKesitVerisi>();

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var bolge in bolgeler)
                {
                    var veri = KesitAlanHesaplaInternal(bolge, referans, tr);
                    if (veri != null)
                        sonuclar.Add(veri);
                }
                tr.Commit();
            }

            LoggingService.Info("Geometrik alan hesabı tamamlandı: {Adet} kesit", sonuclar.Count);
            return sonuclar;
        }

        public GeometrikKesitVerisi KesitAlanHesapla(KesitBolge bolge, ReferansKesitAyarlari referans)
        {
            using (var tr = _documentContext.BeginTransaction())
            {
                var sonuc = KesitAlanHesaplaInternal(bolge, referans, tr);
                tr.Commit();
                return sonuc;
            }
        }

        private GeometrikKesitVerisi KesitAlanHesaplaInternal(
            KesitBolge bolge, ReferansKesitAyarlari referans, Transaction tr)
        {
            try
            {
                var db = _documentContext.Database;
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                // Bounding box içindeki polyline'ları bul ve sınıflandır
                // Parça parça çizgiler olabilir → tümünü birleştir
                var araziParcalar = new List<List<Point2d>>();
                var projeParcalar = new List<List<Point2d>>();
                var siyahKotParcalar = new List<List<Point2d>>();
                var tabakaCizgileri = new List<List<Point2d>>();

                foreach (ObjectId id in ms)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;

                    if (!BoundingBoxIcindeMi(entity, bolge))
                        continue;

                    if (CizgiEslesiyor(entity, referans.AraziCizgisi))
                    {
                        var noktalar = PolylineNoktalariniAl(entity);
                        if (noktalar.Count >= 2)
                            araziParcalar.Add(noktalar);
                    }
                    else if (CizgiEslesiyor(entity, referans.ProjeHatti))
                    {
                        var noktalar = PolylineNoktalariniAl(entity);
                        if (noktalar.Count >= 2)
                            projeParcalar.Add(noktalar);
                    }
                    else if (referans.SiyahKot != null && CizgiEslesiyor(entity, referans.SiyahKot))
                    {
                        var noktalar = PolylineNoktalariniAl(entity);
                        if (noktalar.Count >= 2)
                            siyahKotParcalar.Add(noktalar);
                    }
                    else if (referans.TabakaCizgileri.Count > 0)
                    {
                        foreach (var tabakaTanim in referans.TabakaCizgileri)
                        {
                            if (CizgiEslesiyor(entity, tabakaTanim))
                            {
                                var noktalar = PolylineNoktalariniAl(entity);
                                if (noktalar.Count >= 2)
                                    tabakaCizgileri.Add(noktalar);
                                break;
                            }
                        }
                    }
                }

                // Parçaları birleştir — tüm noktaları X'e göre sırala
                var araziNoktalar = ParcalariXSirayla(araziParcalar);
                var projeNoktalar = ParcalariXSirayla(projeParcalar);
                var siyahKotNoktalar = ParcalariXSirayla(siyahKotParcalar);

                if (araziNoktalar.Count < 2 || projeNoktalar.Count < 2)
                {
                    LoggingService.Warning("Kesit {KM}: Arazi veya proje çizgisi bulunamadı", null,
                        bolge.IstasyonMetni);
                    return null;
                }

                // Kazı/Dolgu alan hesabı

                var (kaziAlani, dolguAlani) = KaziDolguAlanHesapla(araziNoktalar, projeNoktalar);

                var sonuc = new GeometrikKesitVerisi
                {
                    Istasyon = bolge.Istasyon,
                    IstasyonMetni = bolge.IstasyonMetni,
                    KaziAlani = kaziAlani,
                    DolguAlani = dolguAlani
                };

                // Üstyapı alan hesabı
                if (siyahKotNoktalar.Count >= 2)
                {
                    sonuc.UstyapiToplamAlani = IkiCizgiArasiAlan(projeNoktalar, siyahKotNoktalar);

                    if (tabakaCizgileri.Count > 0)
                    {
                        sonuc.TabakaCizgisiKullanildi = true;
                        sonuc.TabakaAlanlari = TabakaCizgilerindenAlanHesapla(
                            projeNoktalar, siyahKotNoktalar, tabakaCizgileri);
                    }
                    else
                    {
                        sonuc.TabakaCizgisiKullanildi = false;
                        sonuc.TabakaAlanlari = TabakaOranindanAlanHesapla(
                            sonuc.UstyapiToplamAlani, referans.Kalinliklar);
                    }
                }

                // Yarma ve Dolgu'yu da tabaka listesine ekle
                sonuc.TabakaAlanlari.Insert(0, new TabakaAlani
                {
                    MalzemeAdi = "Yarma",
                    Alan = kaziAlani,
                    Tahmini = false
                });
                sonuc.TabakaAlanlari.Insert(1, new TabakaAlani
                {
                    MalzemeAdi = "Dolgu",
                    Alan = dolguAlani,
                    Tahmini = false
                });

                return sonuc;
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("Geometrik alan hesabı hatası (Km {KM}): {Hata}", ex,
                    bolge.IstasyonMetni, ex.Message);
                return null;
            }
        }

        private (double kazi, double dolgu) KaziDolguAlanHesapla(
            List<Point2d> araziNoktalar, List<Point2d> projeNoktalar)
        {
            double minX = Math.Max(araziNoktalar.First().X, projeNoktalar.First().X);
            double maxX = Math.Min(araziNoktalar.Last().X, projeNoktalar.Last().X);

            if (maxX <= minX) return (0, 0);

            double kaziAlani = 0;
            double dolguAlani = 0;

            int adimSayisi = (int)Math.Ceiling((maxX - minX) / SampleAraligi);
            double dx = (maxX - minX) / adimSayisi;

            for (int i = 0; i < adimSayisi; i++)
            {
                double x1 = minX + i * dx;
                double x2 = minX + (i + 1) * dx;

                double araziY1 = InterpolateY(araziNoktalar, x1);
                double araziY2 = InterpolateY(araziNoktalar, x2);
                double projeY1 = InterpolateY(projeNoktalar, x1);
                double projeY2 = InterpolateY(projeNoktalar, x2);

                // Trapez yöntemi ile alan
                double fark1 = araziY1 - projeY1;
                double fark2 = araziY2 - projeY2;

                if (fark1 >= 0 && fark2 >= 0)
                {
                    // Tamamen kazı
                    kaziAlani += (fark1 + fark2) / 2.0 * dx;
                }
                else if (fark1 <= 0 && fark2 <= 0)
                {
                    // Tamamen dolgu
                    dolguAlani += (Math.Abs(fark1) + Math.Abs(fark2)) / 2.0 * dx;
                }
                else
                {
                    // Geçiş noktası — doğrusal interpolasyon ile ayır
                    double t = Math.Abs(fark1) / (Math.Abs(fark1) + Math.Abs(fark2));
                    double xKesisim = x1 + t * dx;

                    if (fark1 > 0)
                    {
                        kaziAlani += Math.Abs(fark1) / 2.0 * (xKesisim - x1);
                        dolguAlani += Math.Abs(fark2) / 2.0 * (x2 - xKesisim);
                    }
                    else
                    {
                        dolguAlani += Math.Abs(fark1) / 2.0 * (xKesisim - x1);
                        kaziAlani += Math.Abs(fark2) / 2.0 * (x2 - xKesisim);
                    }
                }
            }

            return (Math.Round(kaziAlani, 4), Math.Round(dolguAlani, 4));
        }

        private double IkiCizgiArasiAlan(List<Point2d> ustNoktalar, List<Point2d> altNoktalar)
        {
            double minX = Math.Max(ustNoktalar.First().X, altNoktalar.First().X);
            double maxX = Math.Min(ustNoktalar.Last().X, altNoktalar.Last().X);
            if (maxX <= minX) return 0;

            var polygon = new List<Point2d>();

            // Üst çizgi soldan sağa
            var ustClipped = ClipToXRange(ustNoktalar, minX, maxX);
            polygon.AddRange(ustClipped.OrderBy(p => p.X));

            // Alt çizgi sağdan sola
            var altClipped = ClipToXRange(altNoktalar, minX, maxX);
            polygon.AddRange(altClipped.OrderByDescending(p => p.X));

            return ShoelaceAlan(polygon);
        }

        private List<TabakaAlani> TabakaCizgilerindenAlanHesapla(
            List<Point2d> projeNoktalar, List<Point2d> siyahKotNoktalar,
            List<List<Point2d>> tabakaCizgileri)
        {
            var sonuc = new List<TabakaAlani>();

            // Tabaka çizgilerini Y ortalamasına göre sırala (üstten alta)
            var sirali = tabakaCizgileri
                .Select(c => c.OrderBy(p => p.X).ToList())
                .OrderByDescending(c => c.Average(p => p.Y))
                .ToList();

            // Proje hattı → ilk tabaka → ... → son tabaka → siyah kot arası alanlar
            var ustCizgiler = new List<List<Point2d>> { projeNoktalar };
            ustCizgiler.AddRange(sirali);

            var altCizgiler = new List<List<Point2d>>(sirali);
            altCizgiler.Add(siyahKotNoktalar);

            string[] tabakaAdlari = { "Aşınma", "Binder", "Bitümen", "Plentmiks", "AltTemel" };

            for (int i = 0; i < Math.Min(ustCizgiler.Count, altCizgiler.Count); i++)
            {
                double alan = IkiCizgiArasiAlan(ustCizgiler[i], altCizgiler[i]);
                string ad = i < tabakaAdlari.Length ? tabakaAdlari[i] : $"Tabaka_{i + 1}";

                sonuc.Add(new TabakaAlani
                {
                    MalzemeAdi = ad,
                    Alan = Math.Round(alan, 4),
                    Tahmini = false
                });
            }

            return sonuc;
        }

        private List<TabakaAlani> TabakaOranindanAlanHesapla(
            double toplamUstyapiAlani, UstyapiKalinliklari kalinliklar)
        {
            var oranlar = kalinliklar.TabakaOranlari();
            var sonuc = new List<TabakaAlani>();

            foreach (var kvp in oranlar)
            {
                sonuc.Add(new TabakaAlani
                {
                    MalzemeAdi = kvp.Key,
                    Alan = Math.Round(toplamUstyapiAlani * kvp.Value, 4),
                    Tahmini = true
                });
            }

            return sonuc;
        }

        // --- Yardımcı metodlar (EnKesitAlanService pattern'inden) ---

        private bool BoundingBoxIcindeMi(Entity entity, KesitBolge bolge)
        {
            try
            {
                var extents = entity.GeometricExtents;
                return extents.MaxPoint.X >= bolge.MinX && extents.MinPoint.X <= bolge.MaxX &&
                       extents.MaxPoint.Y >= bolge.MinY && extents.MinPoint.Y <= bolge.MaxY;
            }
            catch
            {
                return false;
            }
        }

        private bool CizgiEslesiyor(Entity entity, CizgiTanimi tanim)
        {
            if (tanim == null) return false;
            if (entity.Layer != tanim.LayerAdi) return false;
            if (tanim.RenkIndex > 0 && entity.ColorIndex != tanim.RenkIndex) return false;
            return true;
        }

        private List<Point2d> PolylineNoktalariniAl(Entity entity)
        {
            var noktalar = new List<Point2d>();

            if (entity is Polyline pl)
            {
                for (int i = 0; i < pl.NumberOfVertices; i++)
                    noktalar.Add(pl.GetPoint2dAt(i));
            }
            else if (entity is Polyline2d pl2d)
            {
                foreach (ObjectId vertexId in pl2d)
                {
                    var vertex = pl2d.Database.TransactionManager.TopTransaction
                        .GetObject(vertexId, OpenMode.ForRead) as Vertex2d;
                    if (vertex != null)
                        noktalar.Add(new Point2d(vertex.Position.X, vertex.Position.Y));
                }
            }
            else if (entity is Line line)
            {
                noktalar.Add(new Point2d(line.StartPoint.X, line.StartPoint.Y));
                noktalar.Add(new Point2d(line.EndPoint.X, line.EndPoint.Y));
            }

            return noktalar;
        }

        private List<Point2d> ClipToXRange(List<Point2d> points, double minX, double maxX)
        {
            var result = new List<Point2d>();
            result.Add(new Point2d(minX, InterpolateY(points, minX)));

            foreach (var p in points)
            {
                if (p.X >= minX && p.X <= maxX)
                    result.Add(p);
            }

            result.Add(new Point2d(maxX, InterpolateY(points, maxX)));
            return result.OrderBy(p => p.X).ToList();
        }

        private double InterpolateY(List<Point2d> points, double x)
        {
            if (points.Count == 0) return 0;
            if (points.Count == 1) return points[0].Y;

            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];

                if ((x >= p1.X && x <= p2.X) || (x >= p2.X && x <= p1.X))
                {
                    if (Math.Abs(p2.X - p1.X) < 1e-10)
                        return (p1.Y + p2.Y) / 2.0;

                    double t = (x - p1.X) / (p2.X - p1.X);
                    return p1.Y + t * (p2.Y - p1.Y);
                }
            }

            return x <= points.First().X ? points.First().Y : points.Last().Y;
        }

        /// <summary>
        /// Birden fazla çizgi parçasını tek bir nokta listesine birleştirir, X'e göre sıralar.
        /// </summary>
        private List<Point2d> ParcalariXSirayla(List<List<Point2d>> parcalar)
        {
            if (parcalar.Count == 0)
                return new List<Point2d>();

            var tumNoktalar = new List<Point2d>();
            foreach (var parca in parcalar)
                tumNoktalar.AddRange(parca);

            // X'e göre sırala, çok yakın noktaları (aynı X ±0.01) birleştir
            return tumNoktalar
                .OrderBy(p => p.X)
                .ToList();
        }

        private double ShoelaceAlan(List<Point2d> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0;

            double alan = 0;
            int n = polygon.Count;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                alan += polygon[i].X * polygon[j].Y;
                alan -= polygon[j].X * polygon[i].Y;
            }

            return Math.Abs(alan) / 2.0;
        }
    }
}
