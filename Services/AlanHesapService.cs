using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public partial class AlanHesapService : IAlanHesapService
    {
        private readonly IDocumentContext _documentContext;

        public AlanHesapService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public List<AlanOlcumu> Hesapla(SelectionSet secim)
        {
            if (secim == null) return new List<AlanOlcumu>();
            return Hesapla(secim.GetObjectIds());
        }

        public List<AlanOlcumu> Hesapla(IEnumerable<ObjectId> nesneler)
        {
            var sonuclar = new List<AlanOlcumu>();
            if (nesneler == null) return sonuclar;

            using (var tr = _documentContext.BeginTransaction())
            {
                var islenmemisEgriler = new List<(ObjectId Id, Curve Egri)>();

                foreach (var id in nesneler)
                {
                    try
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;

                        double alan = 0;
                        double cevre = 0;
                        string nesneTipi = entity.GetType().Name;
                        bool bireyselIslem = true;

                        switch (entity)
                        {
                            case Polyline pl:
                                if (!pl.Closed && pl.NumberOfVertices >= 3)
                                {
                                    // Açık polyline: başlangıç-bitiş arası kapatılmış gibi alan hesapla
                                    alan = pl.Area;
                                    cevre = pl.Length;
                                    nesneTipi = "Polyline (açık)";
                                    // Ayrıca birleşik bölge denemesi için de ekle
                                    islenmemisEgriler.Add((id, pl));
                                }
                                else if (!pl.Closed)
                                {
                                    islenmemisEgriler.Add((id, pl));
                                    bireyselIslem = false;
                                    break;
                                }
                                else
                                {
                                    alan = pl.Area;
                                    cevre = pl.Length;
                                    nesneTipi = "Polyline";
                                }
                                break;

                            case Polyline2d pl2d:
                                if (!pl2d.Closed && pl2d.Area > Constants.AlanToleransi)
                                {
                                    alan = pl2d.Area;
                                    cevre = pl2d.Length;
                                    nesneTipi = "Polyline2d (açık)";
                                    islenmemisEgriler.Add((id, pl2d));
                                }
                                else if (!pl2d.Closed)
                                {
                                    islenmemisEgriler.Add((id, pl2d));
                                    bireyselIslem = false;
                                    break;
                                }
                                else
                                {
                                    alan = pl2d.Area;
                                    cevre = pl2d.Length;
                                    nesneTipi = "Polyline2d";
                                }
                                break;

                            case Line line:
                                islenmemisEgriler.Add((id, line));
                                bireyselIslem = false;
                                break;

                            case Arc arc:
                                islenmemisEgriler.Add((id, arc));
                                bireyselIslem = false;
                                break;

                            case Spline spline:
                                islenmemisEgriler.Add((id, spline));
                                bireyselIslem = false;
                                break;

                            case Circle circle:
                                alan = Math.PI * circle.Radius * circle.Radius;
                                cevre = 2.0 * Math.PI * circle.Radius;
                                nesneTipi = "Circle";
                                break;

                            case Ellipse ellipse:
                                alan = Math.PI * ellipse.MajorRadius * ellipse.MinorRadius;
                                double a = ellipse.MajorRadius;
                                double b = ellipse.MinorRadius;
                                cevre = Math.PI * (3.0 * (a + b) - Math.Sqrt((3.0 * a + b) * (a + 3.0 * b)));
                                nesneTipi = "Ellipse";
                                break;

                            case Hatch hatch:
                                alan = hatch.Area;
                                nesneTipi = "Hatch";
                                break;

                            case Region region:
                                alan = region.Area;
                                nesneTipi = "Region";
                                break;

                            case Face face:
                                var p0 = face.GetVertexAt(0);
                                var p1f = face.GetVertexAt(1);
                                var p2f = face.GetVertexAt(2);
                                var p3f = face.GetVertexAt(3);
                                alan = Math.Abs(
                                    (p0.X * p1f.Y - p1f.X * p0.Y) +
                                    (p1f.X * p2f.Y - p2f.X * p1f.Y) +
                                    (p2f.X * p3f.Y - p3f.X * p2f.Y) +
                                    (p3f.X * p0.Y - p0.X * p3f.Y)) / 2.0;
                                nesneTipi = "3DFace";
                                break;

                            default:
                                continue;
                        }

                        if (bireyselIslem && alan > Constants.AlanToleransi)
                        {
                            sonuclar.Add(new AlanOlcumu
                            {
                                Alan = alan,
                                BirimAlan = alan,
                                Cevre = cevre,
                                Deger = alan,
                                NesneTipi = nesneTipi,
                                KatmanAdi = entity.Layer,
                                KaynakNesneler = new List<ObjectId> { id },
                                Aciklama = $"{nesneTipi} - {entity.Layer}"
                            });
                        }
                    }
                    catch (System.Exception ex)
                    {
                        LoggingService.Warning("Alan hesaplama hatası", ex);
                    }
                }

                // Geçiş 2: İşlenmemiş eğrileri birleştirerek kapalı bölge oluştur
                if (islenmemisEgriler.Count > 0)
                {
                    BirlesikBolgeHesapla(islenmemisEgriler, sonuclar);
                }

                tr.Commit();
            }

            return sonuclar;
        }

        private void BirlesikBolgeHesapla(
            List<(ObjectId Id, Curve Egri)> egriler,
            List<AlanOlcumu> sonuclar)
        {
            var curves = new DBObjectCollection();
            foreach (var (id, egri) in egriler)
                curves.Add(egri);

            try
            {
                var regions = Region.CreateFromCurves(curves);
                if (regions.Count > 0)
                {
                    var kaynakIdler = egriler.Select(e => e.Id).ToList();
                    var katmanAdi = egriler
                        .GroupBy(e => e.Egri.Layer)
                        .OrderByDescending(g => g.Count())
                        .First().Key;

                    for (int i = 0; i < regions.Count; i++)
                    {
                        var region = regions[i] as Region;
                        if (region != null && region.Area > Constants.AlanToleransi)
                        {
                            double cevre = BolgeCevreHesapla(region);

                            sonuclar.Add(new AlanOlcumu
                            {
                                Alan = region.Area,
                                BirimAlan = region.Area,
                                Cevre = cevre,
                                Deger = region.Area,
                                NesneTipi = "Birleşik Bölge",
                                KatmanAdi = katmanAdi,
                                KaynakNesneler = kaynakIdler,
                                Aciklama = $"Birleşik Bölge ({egriler.Count} eğri) - {katmanAdi}"
                            });
                        }
                        region?.Dispose();
                    }

                    LoggingService.Info(
                        "Birleşik bölge oluşturuldu: {BolgeCount} bölge, {EgriCount} eğriden",
                        regions.Count, egriler.Count);
                }
            }
            catch (System.Exception ex)
            {
                LoggingService.Warning(
                    "Eğriler birleştirilemedi, tek başına kapalı eğriler deneniyor", ex);
                TekBasinaKapaliEgriDene(egriler, sonuclar);
            }
        }

        private double BolgeCevreHesapla(Region region)
        {
            double cevre = 0;
            var explodedObjects = new DBObjectCollection();
            region.Explode(explodedObjects);

            foreach (DBObject obj in explodedObjects)
            {
                if (obj is Curve curve)
                    cevre += curve.GetDistanceAtParameter(curve.EndParam);
                obj.Dispose();
            }

            return cevre;
        }

        private void TekBasinaKapaliEgriDene(
            List<(ObjectId Id, Curve Egri)> egriler,
            List<AlanOlcumu> sonuclar)
        {
            foreach (var (id, egri) in egriler)
            {
                if (!egri.Closed) continue;

                try
                {
                    var tekCurve = new DBObjectCollection { egri };
                    var regions = Region.CreateFromCurves(tekCurve);
                    if (regions.Count > 0)
                    {
                        var region = regions[0] as Region;
                        if (region != null && region.Area > Constants.AlanToleransi)
                        {
                            double cevre = BolgeCevreHesapla(region);
                            sonuclar.Add(new AlanOlcumu
                            {
                                Alan = region.Area,
                                BirimAlan = region.Area,
                                Cevre = cevre,
                                Deger = region.Area,
                                NesneTipi = egri.GetType().Name,
                                KatmanAdi = egri.Layer,
                                KaynakNesneler = new List<ObjectId> { id },
                                Aciklama = $"{egri.GetType().Name} (kapalı) - {egri.Layer}"
                            });
                        }

                        for (int i = 0; i < regions.Count; i++)
                            ((DBObject)regions[i]).Dispose();
                    }
                }
                catch (System.Exception ex)
                {
                    LoggingService.Warning(
                        $"Kapalı eğri ({egri.GetType().Name}) Region'a dönüştürülemedi", ex);
                }
            }
        }

        public double BirimDonustur(double metrekare, BirimTipi hedefBirim)
        {
            switch (hedefBirim)
            {
                case BirimTipi.Hektar:
                    return metrekare / 10000.0;
                case BirimTipi.Donum:
                    return metrekare / 1000.0;
                case BirimTipi.Metrekare:
                default:
                    return metrekare;
            }
        }
    }
}
