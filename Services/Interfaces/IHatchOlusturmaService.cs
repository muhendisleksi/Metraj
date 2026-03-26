using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IHatchOlusturmaService
    {
        (ObjectId hatchId, double alan) HatchOlustur(Point3d nokta, MalzemeHatchAyari ayar);
        ObjectId EtiketYaz(ObjectId hatchId, string kolonHarfi, MalzemeHatchAyari ayar);
        void KolonHatchTemizle(string kolonHarfi);
        void TumHatchTemizle();
    }
}
