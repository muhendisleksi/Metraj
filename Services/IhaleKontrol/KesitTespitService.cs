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
    public class KesitTespitService : IKesitTespitService
    {
        private readonly IDocumentContext _documentContext;

        // Grid tespiti için tolerans (aynı satır/sütunda kabul mesafesi)
        private const double SatirToleransi = 50.0;
        private const double SutunToleransi = 50.0;

        public KesitTespitService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public List<KesitBolge> KesitleriBelirle(List<TabloKesitVerisi> tablolar, ReferansKesitAyarlari referans)
        {
            if (tablolar == null || tablolar.Count == 0)
                return new List<KesitBolge>();

            // 1. Tablo konumlarından grid düzeni çıkar
            var grid = GridDuzeniCikar(tablolar);

            // 2. Her tablo için bounding box hesapla
            var bolgeler = new List<KesitBolge>();

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var tablo in tablolar)
                {
                    var bolge = BoundingBoxHesapla(tablo, grid, referans, tr);
                    if (bolge != null)
                        bolgeler.Add(bolge);
                }
                tr.Commit();
            }

            bolgeler.Sort((a, b) => a.Istasyon.CompareTo(b.Istasyon));
            LoggingService.Info("Kesit tespiti tamamlandı: {Adet} kesit bölgesi belirlendi", bolgeler.Count);
            return bolgeler;
        }

        private GridDuzeni GridDuzeniCikar(List<TabloKesitVerisi> tablolar)
        {
            // Y koordinatlarını grupla → satırlar
            var yDegerleri = tablolar.Select(t => t.TabloY).OrderByDescending(y => y).ToList();
            var satirlar = Grupla(yDegerleri, SatirToleransi);

            // X koordinatlarını grupla → sütunlar
            var xDegerleri = tablolar.Select(t => t.TabloX).OrderBy(x => x).ToList();
            var sutunlar = Grupla(xDegerleri, SutunToleransi);

            var grid = new GridDuzeni
            {
                SatirYDegerleri = satirlar,
                SutunXDegerleri = sutunlar
            };

            // Her tablo için satır/sütun indeksini bul
            foreach (var tablo in tablolar)
            {
                int satir = EnYakinGrupBul(satirlar, tablo.TabloY);
                int sutun = EnYakinGrupBul(sutunlar, tablo.TabloX);
                grid.TabloKonumlari[tablo] = (satir, sutun);
            }

            LoggingService.Info("Grid düzeni: {Satir} satır × {Sutun} sütun", satirlar.Count, sutunlar.Count);
            return grid;
        }

        private List<double> Grupla(List<double> degerler, double tolerans)
        {
            var gruplar = new List<double>();

            foreach (double deger in degerler)
            {
                bool grupBulundu = false;
                for (int i = 0; i < gruplar.Count; i++)
                {
                    if (Math.Abs(gruplar[i] - deger) < tolerans)
                    {
                        // Grubun ortalamasını güncelle
                        gruplar[i] = (gruplar[i] + deger) / 2.0;
                        grupBulundu = true;
                        break;
                    }
                }
                if (!grupBulundu)
                    gruplar.Add(deger);
            }

            return gruplar;
        }

        private int EnYakinGrupBul(List<double> gruplar, double deger)
        {
            int enYakinIdx = 0;
            double enKucukFark = double.MaxValue;

            for (int i = 0; i < gruplar.Count; i++)
            {
                double fark = Math.Abs(gruplar[i] - deger);
                if (fark < enKucukFark)
                {
                    enKucukFark = fark;
                    enYakinIdx = i;
                }
            }

            return enYakinIdx;
        }

        private KesitBolge BoundingBoxHesapla(TabloKesitVerisi tablo, GridDuzeni grid,
            ReferansKesitAyarlari referans, Transaction tr)
        {
            if (!grid.TabloKonumlari.TryGetValue(tablo, out var konum))
                return null;

            int satir = konum.satir;
            int sutun = konum.sutun;

            // Tablonun sol kenarı ≈ kesitin sağ sınırı
            // Kesitin sol sınırı: önceki sütundaki tablonun sağ kenarı veya grid başlangıcı
            double kesitSag = tablo.TabloX;
            double kesitSol;

            if (sutun > 0)
            {
                // Bir önceki sütundaki en yakın tablonun X'ini bul
                var oncekiSutunTablolari = grid.TabloKonumlari
                    .Where(kvp => kvp.Value.sutun == sutun - 1 && kvp.Value.satir == satir)
                    .Select(kvp => kvp.Key.TabloX)
                    .ToList();

                if (oncekiSutunTablolari.Count > 0)
                    kesitSol = oncekiSutunTablolari.Average() + 20; // Tahmini tablo genişliği offset
                else
                    kesitSol = kesitSag - 200; // Varsayılan kesit genişliği
            }
            else
            {
                kesitSol = kesitSag - 200;
            }

            // Dikey sınırlar: satır yüksekliğinden tahmin
            double satırY = grid.SatirYDegerleri[satir];
            double kesitUst, kesitAlt;

            if (satir > 0)
            {
                double ustSatirY = grid.SatirYDegerleri[satir - 1];
                double aralik = Math.Abs(ustSatirY - satırY);
                kesitUst = satırY + aralik * 0.1;
                kesitAlt = satırY - aralik * 0.9;
            }
            else
            {
                kesitUst = satırY + 30;
                kesitAlt = satırY - 150;
            }

            // CL çizgisini bounding box içinde ara
            double clX = (kesitSol + kesitSag) / 2.0; // Varsayılan: ortada
            if (referans != null && referans.CLCizgisi != null)
            {
                double bulunanCL = CLCizgisiBul(kesitSol, kesitSag, kesitAlt, kesitUst, referans.CLCizgisi, tr);
                if (bulunanCL > 0)
                    clX = bulunanCL;
            }

            return new KesitBolge
            {
                Istasyon = tablo.Istasyon,
                IstasyonMetni = tablo.IstasyonMetni,
                MinX = kesitSol,
                MinY = kesitAlt,
                MaxX = kesitSag,
                MaxY = kesitUst,
                CLX = clX,
                TabloVerisi = tablo,
                Satir = satir,
                Sutun = sutun
            };
        }

        private double CLCizgisiBul(double minX, double maxX, double minY, double maxY,
            CizgiTanimi clTanim, Transaction tr)
        {
            try
            {
                var db = _documentContext.Database;
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                double enIyiX = -1;
                double enIyiMerkez = (minX + maxX) / 2.0;
                double enKucukFark = double.MaxValue;

                foreach (ObjectId id in ms)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;

                    // Layer ve renk filtresi
                    if (entity.Layer != clTanim.LayerAdi) continue;
                    if (clTanim.RenkIndex > 0 && entity.ColorIndex != clTanim.RenkIndex) continue;

                    // Line objesi (CL genelde dikey bir çizgi)
                    if (entity is Line line)
                    {
                        double lineX = (line.StartPoint.X + line.EndPoint.X) / 2.0;
                        double lineMinY = Math.Min(line.StartPoint.Y, line.EndPoint.Y);
                        double lineMaxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y);

                        // Bounding box içinde mi?
                        if (lineX >= minX && lineX <= maxX &&
                            lineMaxY >= minY && lineMinY <= maxY)
                        {
                            double fark = Math.Abs(lineX - enIyiMerkez);
                            if (fark < enKucukFark)
                            {
                                enKucukFark = fark;
                                enIyiX = lineX;
                            }
                        }
                    }
                }

                return enIyiX;
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning("CL çizgisi arama hatası: {Hata}", ex, ex.Message);
                return -1;
            }
        }

        private class GridDuzeni
        {
            public List<double> SatirYDegerleri { get; set; } = new List<double>();
            public List<double> SutunXDegerleri { get; set; } = new List<double>();
            public Dictionary<TabloKesitVerisi, (int satir, int sutun)> TabloKonumlari { get; set; }
                = new Dictionary<TabloKesitVerisi, (int satir, int sutun)>();
        }
    }
}
