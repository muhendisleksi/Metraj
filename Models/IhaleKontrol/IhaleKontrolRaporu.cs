using System;
using System.Collections.Generic;
using System.Linq;

namespace Metraj.Models.IhaleKontrol
{
    public class IhaleKontrolRaporu
    {
        public string ProjeAdi { get; set; }
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;

        public List<TabloKesitVerisi> TabloVerileri { get; set; } = new List<TabloKesitVerisi>();
        public List<GeometrikKesitVerisi> GeometrikVeriler { get; set; } = new List<GeometrikKesitVerisi>();
        public List<KesitKarsilastirma> Karsilastirmalar { get; set; } = new List<KesitKarsilastirma>();

        public KubajKarsilastirma KubajSonucu { get; set; }

        // Tolerans ayarları
        public double UyariToleransYuzde { get; set; } = 3.0;
        public double HataToleransYuzde { get; set; } = 10.0;
        public double MutlakTolerans { get; set; } = 0.1;

        // Özet istatistikler
        public int ToplamKesit => Karsilastirmalar.Count;
        public int SorunsuzKesit => Karsilastirmalar.Count(k => k.Durum == KontrolDurumu.OK);
        public int UyariKesit => Karsilastirmalar.Count(k => k.Durum == KontrolDurumu.Uyari);
        public int HataliKesit => Karsilastirmalar.Count(k => k.Durum == KontrolDurumu.Hata);
    }

    public class KubajKarsilastirma
    {
        public List<MalzemeKubajKarsilastirma> MalzemeKubajlari { get; set; } = new List<MalzemeKubajKarsilastirma>();
    }

    public class MalzemeKubajKarsilastirma
    {
        public string MalzemeAdi { get; set; }
        public double IhaleHacmi { get; set; }
        public double HesapHacmi { get; set; }
        public double Fark { get; set; }
        public double FarkYuzde { get; set; }
        public KontrolDurumu Durum { get; set; }
    }
}
