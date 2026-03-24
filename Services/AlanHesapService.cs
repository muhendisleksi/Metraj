using System;
using System.Collections.Generic;
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
                foreach (var id in nesneler)
                {
                    try
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;

                        double alan = 0;
                        double cevre = 0;
                        string nesneTipi = entity.GetType().Name;

                        switch (entity)
                        {
                            case Polyline pl:
                                if (!pl.Closed) continue;
                                alan = pl.Area;
                                cevre = pl.Length;
                                nesneTipi = "Polyline";
                                break;

                            case Polyline2d pl2d:
                                if (!pl2d.Closed) continue;
                                alan = pl2d.Area;
                                cevre = pl2d.Length;
                                nesneTipi = "Polyline2d";
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

                            default:
                                continue;
                        }

                        if (alan > Constants.AlanToleransi)
                        {
                            sonuclar.Add(new AlanOlcumu
                            {
                                Alan = alan,
                                BirimAlan = alan,
                                Cevre = cevre,
                                Deger = alan,
                                NesneTipi = nesneTipi,
                                KatmanAdi = entity.Layer,
                                KaynakNesneler = new System.Collections.Generic.List<ObjectId> { id },
                                Aciklama = $"{nesneTipi} - {entity.Layer}"
                            });
                        }
                    }
                    catch (System.Exception ex)
                    {
                        LoggingService.Warning("Alan hesaplama hatası", ex);
                    }
                }

                tr.Commit();
            }

            return sonuclar;
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
