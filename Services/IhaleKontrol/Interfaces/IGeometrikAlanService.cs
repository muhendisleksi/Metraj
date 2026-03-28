using System.Collections.Generic;
using Metraj.Models.IhaleKontrol;

namespace Metraj.Services.IhaleKontrol.Interfaces
{
    public interface IGeometrikAlanService
    {
        GeometrikKesitVerisi KesitAlanHesapla(KesitBolge bolge, ReferansKesitAyarlari referans);
        List<GeometrikKesitVerisi> TumKesitleriHesapla(List<KesitBolge> bolgeler, ReferansKesitAyarlari referans);
    }
}
