using System.Collections.Generic;

namespace Metraj.Models.IhaleKontrol
{
    public class KesitKarsilastirma
    {
        public double Istasyon { get; set; }
        public string IstasyonMetni { get; set; }
        public List<MalzemeKarsilastirma> Malzemeler { get; set; } = new List<MalzemeKarsilastirma>();

        public double ToplamTabloDegeri { get; set; }
        public double ToplamGeometrikDeger { get; set; }
        public double ToplamFark { get; set; }
        public double ToplamFarkYuzde { get; set; }
        public KontrolDurumu Durum { get; set; }
    }

    public class MalzemeKarsilastirma
    {
        public string MalzemeAdi { get; set; }
        public double TabloDegeri { get; set; }
        public double GeometrikDeger { get; set; }
        public double Fark { get; set; }
        public double FarkYuzde { get; set; }
        public bool GeometrikHesapYapildi { get; set; }
        public bool Tahmini { get; set; }
        public KontrolDurumu Durum { get; set; }
    }

    public enum KontrolDurumu
    {
        OK,
        Uyari,
        Hata,
        DogrulamaYok
    }
}
