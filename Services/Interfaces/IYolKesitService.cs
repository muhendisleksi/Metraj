using System.Collections.Generic;
using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IYolKesitService
    {
        YolKesitVerisi TiklaIsaretleKesitOku(string kolonHarfi);
        List<KatmanAlanBilgisi> KalemEkle(string kolonHarfi);
        double AlanDuzelt(string kolonHarfi, string malzemeAdi, bool ekleme);
        string KolonHarfiUret(int sira);
        double IstasyonParse(string metin);
    }
}
