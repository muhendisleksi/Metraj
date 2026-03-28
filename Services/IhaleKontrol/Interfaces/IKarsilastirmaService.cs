using System.Collections.Generic;
using Metraj.Models.IhaleKontrol;

namespace Metraj.Services.IhaleKontrol.Interfaces
{
    public interface IKarsilastirmaService
    {
        List<KesitKarsilastirma> Karsilastir(
            List<TabloKesitVerisi> tabloVerileri,
            List<GeometrikKesitVerisi> geometrikVeriler,
            double uyariTolerans = 3.0,
            double hataTolerans = 10.0,
            double mutlakTolerans = 0.1);

        KubajKarsilastirma KubajKarsilastir(
            List<TabloKesitVerisi> tabloVerileri,
            List<GeometrikKesitVerisi> geometrikVeriler,
            HacimMetoduSecimi metot = HacimMetoduSecimi.OrtalamaAlan);
    }

    public enum HacimMetoduSecimi
    {
        OrtalamaAlan,
        Prismoidal
    }
}
