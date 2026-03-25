using System.Collections.Generic;

namespace Metraj.Models
{
    public class KatmanHacimSegmenti
    {
        public string MalzemeAdi { get; set; }
        public double Istasyon1 { get; set; }
        public double Istasyon2 { get; set; }
        public double Alan1 { get; set; }               // m² - ilk kesitteki alan
        public double Alan2 { get; set; }               // m² - ikinci kesitteki alan
        public double Hacim { get; set; }               // m³
        public double Mesafe { get; set; }              // m - istasyonlar arası
        public double TatbikMesafesi { get; set; }      // m - bu katman için tatbik mesafesi
    }

    public class MalzemeHacimOzeti
    {
        public string MalzemeAdi { get; set; }
        public MalzemeKategorisi Kategori { get; set; }
        public double ToplamHacim { get; set; }         // m³
        public List<KatmanHacimSegmenti> Segmentler { get; set; } = new List<KatmanHacimSegmenti>();
    }

    public class YolKubajSonucu
    {
        public HacimMetodu Metot { get; set; }
        public List<MalzemeHacimOzeti> MalzemeOzetleri { get; set; } = new List<MalzemeHacimOzeti>();
        public double ToplamKaziHacmi { get; set; }     // m³
        public double ToplamDolguHacmi { get; set; }    // m³
        public double NetHacim { get; set; }            // m³ (kazı - dolgu)
        public List<BrucknerNoktasi> BrucknerVerisi { get; set; } = new List<BrucknerNoktasi>();
    }
}
