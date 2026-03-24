using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Infrastructure.AutoCAD;
using Metraj.Models;
using Metraj.Services.Interfaces;

namespace Metraj.Services
{
    public class AnnotationService : IAnnotationService
    {
        private readonly IDocumentContext _documentContext;
        private readonly IEntityService _entityService;

        public AnnotationService(IDocumentContext documentContext, IEntityService entityService)
        {
            _documentContext = documentContext;
            _entityService = entityService;
        }

        public ObjectId YaziYaz(Point3d konum, string metin, AnnotationAyarlari ayarlar)
        {
            if (string.IsNullOrWhiteSpace(metin))
                return ObjectId.Null;

            ayarlar = ayarlar ?? new AnnotationAyarlari();

            ObjectId resultId = ObjectId.Null;

            _documentContext.LockDocument(db =>
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // Ensure layer exists
                    short colorIndex = Constants.RenkEtiket;
                    if (ayarlar.KatmanAdi == Constants.LayerUzunluk) colorIndex = Constants.RenkUzunluk;
                    else if (ayarlar.KatmanAdi == Constants.LayerAlan) colorIndex = Constants.RenkAlan;
                    else if (ayarlar.KatmanAdi == Constants.LayerHacim) colorIndex = Constants.RenkHacim;
                    else if (ayarlar.KatmanAdi == Constants.LayerToplama) colorIndex = Constants.RenkToplama;

                    _entityService.EnsureLayer(tr, db, ayarlar.KatmanAdi, colorIndex);

                    // Create MText
                    var mtext = new MText();
                    mtext.Location = konum;
                    mtext.Contents = metin;
                    mtext.TextHeight = ayarlar.TextYuksekligi;
                    mtext.Layer = ayarlar.KatmanAdi;

                    resultId = _entityService.AddEntity(tr, mtext, ayarlar.KatmanAdi);

                    tr.Commit();
                }
            });

            LoggingService.Info("Annotasyon yazildi: {Metin} at ({X:F2}, {Y:F2})", metin, konum.X, konum.Y);
            return resultId;
        }

        public void AnnotasyonlariTemizle(string katmanAdi)
        {
            _documentContext.LockDocument(db =>
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var entities = _entityService.GetEntitiesOnLayer(tr, db, katmanAdi);
                    int count = 0;

                    foreach (var id in entities)
                    {
                        _entityService.DeleteEntity(tr, id);
                        count++;
                    }

                    tr.Commit();
                    LoggingService.Info("Annotasyonlar temizlendi: {Count} nesne, katman {Layer}", count, katmanAdi);
                }
            });
        }
    }
}
