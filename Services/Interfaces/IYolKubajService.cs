using System.Collections.Generic;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IYolKubajService
    {
        YolKubajSonucu KubajHesapla(List<YolKesitVerisi> kesitler, HacimMetodu metot);
        double SegmentHacimHesapla(double alan1, double alan2, double mesafe, HacimMetodu metot);
        double TatbikMesafesiHesapla(double kaziAlani, double dolguAlani, double mesafe, bool kaziIcin);
        List<BrucknerNoktasi> BrucknerHesapla(List<YolKesitVerisi> kesitler, HacimMetodu metot);
    }
}
