using System.Collections.Generic;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IMalzemeHatchAyarService
    {
        MalzemeHatchAyarlari AyarlariYukle();
        void AyarlariKaydet(MalzemeHatchAyarlari ayarlar);
        MalzemeHatchAyari MalzemeAyariGetir(string malzemeAdi);
        List<string> TumMalzemeAdlari();
    }
}
