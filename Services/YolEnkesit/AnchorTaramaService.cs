using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models.YolEnkesit;
using Metraj.Services;

namespace Metraj.Services.YolEnkesit
{
    public class AnchorTaramaService : IAnchorTaramaService
    {
        private readonly IDocumentContext _documentContext;
        private const double DuplikeMesafeToleransi = 1.0;

        public AnchorTaramaService(IDocumentContext documentContext)
        {
            _documentContext = documentContext;
        }

        public List<AnchorNokta> AnchorTara(IEnumerable<ObjectId> entityIds)
        {
            var anchorlar = new List<AnchorNokta>();

            using (var tr = _documentContext.BeginTransaction())
            {
                foreach (var id in entityIds)
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);
                    string metin = null;
                    double x = 0, y = 0;

                    if (obj is DBText dbText)
                    {
                        metin = dbText.TextString;
                        x = dbText.Position.X;
                        y = dbText.Position.Y;
                    }
                    else if (obj is MText mText)
                    {
                        metin = mText.Contents;
                        x = mText.Location.X;
                        y = mText.Location.Y;
                    }
                    else
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(metin)) continue;

                    double istasyon = YolKesitService.IstasyonParseStatik(metin);
                    if (istasyon < 0) continue;

                    anchorlar.Add(new AnchorNokta
                    {
                        Istasyon = istasyon,
                        IstasyonMetni = metin.Trim(),
                        X = x,
                        Y = y,
                        TextId = id
                    });
                }

                tr.Commit();
            }

            anchorlar = anchorlar.OrderBy(a => a.Istasyon).ToList();
            anchorlar = DuplikeTemizle(anchorlar);

            LoggingService.Info($"Anchor tarama: {anchorlar.Count} istasyon bulundu");
            return anchorlar;
        }

        private List<AnchorNokta> DuplikeTemizle(List<AnchorNokta> anchorlar)
        {
            if (anchorlar.Count <= 1) return anchorlar;

            var sonuc = new List<AnchorNokta>();
            var gruplar = anchorlar.GroupBy(a => Math.Round(a.Istasyon, 1));

            foreach (var grup in gruplar)
            {
                if (grup.Count() == 1)
                {
                    sonuc.Add(grup.First());
                }
                else
                {
                    var ortalama = grup.Average(a => a.Y);
                    var enYakin = grup.OrderBy(a => Math.Abs(a.Y - ortalama)).First();
                    sonuc.Add(enYakin);
                }
            }

            return sonuc.OrderBy(a => a.Istasyon).ToList();
        }
    }
}
