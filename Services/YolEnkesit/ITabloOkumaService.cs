using System.Collections.Generic;
using System.Text;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    public interface ITabloOkumaService
    {
        Dictionary<string, double> TabloOku(KesitGrubu kesit);
        List<TabloKiyasSonucu> Kiyasla(KesitGrubu kesit);
        void TopluKiyasla(List<KesitGrubu> kesitler);

        /// <summary>Tanilama raporuna text detaylarini yazar.</summary>
        void TabloTanilamaYaz(KesitGrubu kesit, StringBuilder sb);
    }
}
