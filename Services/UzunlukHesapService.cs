using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public partial class UzunlukHesapService : IUzunlukHesapService
    {
        private readonly IDocumentContext _documentContext;

        public UzunlukHesapService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public List<UzunlukOlcumu> Hesapla(SelectionSet secim)
        {
            if (secim == null) return new List<UzunlukOlcumu>();
            return Hesapla(secim.GetObjectIds());
        }

        public List<UzunlukOlcumu> Hesapla(IEnumerable<ObjectId> nesneler)
        {
            var sonuclar = new List<UzunlukOlcumu>();
            if (nesneler == null) return sonuclar;

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var id in nesneler)
                {
                    try
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;

                        var uzunluk = EntityUzunlukHesapla(entity);
                        if (uzunluk > Constants.UzunlukToleransi)
                        {
                            sonuclar.Add(new UzunlukOlcumu
                            {
                                Uzunluk = uzunluk,
                                Deger = uzunluk,
                                NesneTipi = GetNesneTipiAdi(entity),
                                KatmanAdi = entity.Layer,
                                RenkIndeksi = (short)entity.ColorIndex,
                                KaynakNesneler = new List<ObjectId> { id },
                                Aciklama = $"{GetNesneTipiAdi(entity)} - {entity.Layer}"
                            });
                        }
                    }
                    catch (System.Exception ex)
                    {
                        LoggingService.Warning("Uzunluk hesaplama hatası", ex);
                    }
                }

                tr.Commit();
            }

            return sonuclar;
        }

        private double EntityUzunlukHesapla(Entity entity)
        {
            switch (entity)
            {
                case Line line:
                    return line.StartPoint.DistanceTo(line.EndPoint);

                case Polyline pl:
                    return pl.Length;

                case Polyline2d pl2d:
                    return pl2d.Length;

                case Polyline3d pl3d:
                    return pl3d.Length;

                case Arc arc:
                    return arc.Length;

                case Circle circle:
                    return 2.0 * Math.PI * circle.Radius;

                case Spline spline:
                    return spline.GetDistanceAtParameter(spline.EndParam);

                case Ellipse ellipse:
                    // Ramanujan approximation
                    double a = ellipse.MajorRadius;
                    double b = ellipse.MinorRadius;
                    return Math.PI * (3.0 * (a + b) - Math.Sqrt((3.0 * a + b) * (a + 3.0 * b)));

                default:
                    return 0.0;
            }
        }

        private string GetNesneTipiAdi(Entity entity)
        {
            switch (entity)
            {
                case Line _: return "Line";
                case Polyline _: return "Polyline";
                case Polyline2d _: return "Polyline2d";
                case Polyline3d _: return "Polyline3d";
                case Arc _: return "Arc";
                case Circle _: return "Circle";
                case Spline _: return "Spline";
                case Ellipse _: return "Ellipse";
                default: return entity.GetType().Name;
            }
        }

        public Dictionary<string, List<UzunlukOlcumu>> Grupla(List<UzunlukOlcumu> sonuclar, GruplamaTipi gruplama)
        {
            if (sonuclar == null || sonuclar.Count == 0)
                return new Dictionary<string, List<UzunlukOlcumu>>();

            switch (gruplama)
            {
                case GruplamaTipi.Katman:
                    return sonuclar.GroupBy(s => s.KatmanAdi ?? "Bilinmeyen")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GruplamaTipi.Renk:
                    return sonuclar.GroupBy(s => s.RenkIndeksi.ToString())
                        .ToDictionary(g => "Renk " + g.Key, g => g.ToList());

                case GruplamaTipi.NesneTipi:
                    return sonuclar.GroupBy(s => s.NesneTipi ?? "Bilinmeyen")
                        .ToDictionary(g => g.Key, g => g.ToList());

                default:
                    return new Dictionary<string, List<UzunlukOlcumu>>
                    {
                        { "Tümü", sonuclar }
                    };
            }
        }
    }
}
