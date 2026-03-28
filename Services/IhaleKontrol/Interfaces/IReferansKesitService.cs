using System.Collections.Generic;
using Metraj.Models.IhaleKontrol;

namespace Metraj.Services.IhaleKontrol.Interfaces
{
    public interface IReferansKesitService
    {
        ReferansKesitAyarlari ReferansKesitTanimla();
        void AyarlariKaydet(ReferansKesitAyarlari ayarlar);
        ReferansKesitAyarlari AyarlariYukle(string projeAdi);
        List<string> KayitliProfilleriListele();
    }
}
