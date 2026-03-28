using System.Collections.Generic;
using Metraj.Models.YolEnkesit;

namespace Metraj.Services.YolEnkesit
{
    public interface IKesitAlanHesapService
    {
        List<AlanHesapSonucu> AlanHesapla(KesitGrubu kesit);
        void TopluAlanHesapla(List<KesitGrubu> kesitler);

        /// <summary>5 orneklem kesit icin detayli tanilama raporu yazar (dosyaya).</summary>
        string TanilamaRaporuYaz(List<KesitGrubu> kesitler);
    }
}
