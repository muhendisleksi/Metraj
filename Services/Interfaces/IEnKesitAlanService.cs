using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IEnKesitAlanService
    {
        double IkiCizgiArasiAlan(ObjectId ustCizgi, ObjectId altCizgi);
        double KapaliNesneAlan(ObjectId nesne);
        List<Point2d> PolylineNoktalariniAl(ObjectId entityId);
        EnKesitAlanOlcumu BoundaryAlanHesapla(Point3d nokta);
        string MalzemeAdiCikar(string layerAdi);
    }
}
