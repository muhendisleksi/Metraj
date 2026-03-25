using Metraj.Models;

namespace Metraj.Services.Interfaces
{
    public interface IKatmanEslestirmeService
    {
        KatmanEslestirme LayerEslestir(string layerAdi);
        KatmanEslestirmeAyarlari AyarlariYukle();
        void AyarlariKaydet(KatmanEslestirmeAyarlari ayarlar);
        bool PatternEslesiyor(string layerAdi, string pattern);
    }
}
