using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public class ToplamaService : IToplamaService
    {
        private readonly IDocumentContext _documentContext;

        public ToplamaService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public List<ToplamaOgesi> ToplaMetinleri(SelectionSet secim, string onEkFiltre, string sonEkFiltre)
        {
            var ogeler = new List<ToplamaOgesi>();
            if (secim == null) return ogeler;

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var id in secim.GetObjectIds())
                {
                    try
                    {
                        var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;

                        string metin = null;
                        Autodesk.AutoCAD.Geometry.Point3d konum = default;

                        if (entity is DBText dbText)
                        {
                            metin = dbText.TextString;
                            konum = dbText.Position;
                        }
                        else if (entity is MText mText)
                        {
                            metin = mText.Text;
                            konum = mText.Location;
                        }
                        else
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(metin)) continue;

                        // Store original text for display
                        string orijinalMetin = metin;

                        // Apply prefix filter
                        if (!string.IsNullOrEmpty(onEkFiltre))
                        {
                            if (!metin.TrimStart().StartsWith(onEkFiltre, StringComparison.OrdinalIgnoreCase))
                                continue;
                            metin = NumberParserHelper.StripPrefix(metin, onEkFiltre);
                        }

                        // Apply suffix filter
                        if (!string.IsNullOrEmpty(sonEkFiltre))
                        {
                            if (!metin.TrimEnd().EndsWith(sonEkFiltre, StringComparison.OrdinalIgnoreCase))
                                continue;
                            metin = NumberParserHelper.StripSuffix(metin, sonEkFiltre);
                        }

                        var oge = new ToplamaOgesi
                        {
                            MetinDegeri = orijinalMetin,
                            KaynakObjectId = id,
                            Konum = konum,
                            KatmanAdi = entity.Layer
                        };

                        if (NumberParserHelper.TryParse(metin, out double sayi))
                        {
                            oge.SayisalDeger = sayi;
                            oge.GecerliSayi = true;
                        }
                        else
                        {
                            oge.GecerliSayi = false;
                        }

                        ogeler.Add(oge);
                    }
                    catch (System.Exception ex)
                    {
                        LoggingService.Warning("Metin okuma hatası", ex);
                    }
                }

                tr.Commit();
            }

            return ogeler;
        }

        public double ToplamDeger(List<ToplamaOgesi> ogeler)
        {
            if (ogeler == null) return 0;
            return ogeler.Where(o => o.GecerliSayi).Sum(o => o.SayisalDeger);
        }
    }
}
