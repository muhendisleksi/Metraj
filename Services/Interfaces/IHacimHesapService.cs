using System.Collections.Generic;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IHacimHesapService
    {
        HacimHesapSonucu HesaplaEnkesittenHacim(List<EnKesitVerisi> enkesitler, HacimMetodu metot);
        List<BrucknerNoktasi> BrucknerHesapla(HacimHesapSonucu sonuc);
        double EnkesitAlanHesapla(List<Autodesk.AutoCAD.Geometry.Point2d> profilNoktalari);
    }
}
