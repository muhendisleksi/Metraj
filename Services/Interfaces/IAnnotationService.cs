using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IAnnotationService
    {
        ObjectId YaziYaz(Point3d konum, string metin, AnnotationAyarlari ayarlar);
        void AnnotasyonlariTemizle(string katmanAdi);
    }
}
