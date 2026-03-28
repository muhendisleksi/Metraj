using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IHatchOlusturmaService
    {
        (ObjectId hatchId, double alan) HatchOlustur(Point3d nokta, MalzemeHatchAyari ayar);
        (ObjectId hatchId, double toplamAlan) CokluHatchOlustur(List<Point3d> noktalar, MalzemeHatchAyari ayar, List<ObjectId> nesneEntityIds = null);
        ObjectId EtiketYaz(ObjectId hatchId, string kolonHarfi, MalzemeHatchAyari ayar, Point3d? icNokta = null);
        (ObjectId hatchId, double alan) NesnedenHatchOlustur(ObjectId nesneId, MalzemeHatchAyari ayar);
        void HatchSil(ObjectId hatchId);
        void MalzemeHatchSil(string layerAdi, string etiketLayerAdi, List<double[]> tiklamaNoktalari);
        void KolonHatchTemizle(string kolonHarfi);
        void TumHatchTemizle();
    }
}
