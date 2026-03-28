using System.Collections.Generic;
using Metraj.Models.IhaleKontrol;

namespace Metraj.Services.IhaleKontrol.Interfaces
{
    public interface IKesitTespitService
    {
        List<KesitBolge> KesitleriBelirle(List<TabloKesitVerisi> tablolar, ReferansKesitAyarlari referans);
    }
}
