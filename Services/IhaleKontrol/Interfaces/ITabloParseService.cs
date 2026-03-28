using System.Collections.Generic;
using Metraj.Models.IhaleKontrol;

namespace Metraj.Services.IhaleKontrol.Interfaces
{
    public interface ITabloParseService
    {
        List<TabloKesitVerisi> TumTablolariOku();
        string MalzemeAdiNormalize(string hamAd);
    }
}
