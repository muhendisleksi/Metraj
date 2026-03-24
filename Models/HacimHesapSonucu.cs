using System.Collections.Generic;

namespace Metraj.Models
{
    public class HacimHesapSonucu
    {
        public double ToplamHacim { get; set; }
        public HacimMetodu Metot { get; set; }
        public List<HacimSegmenti> Segmentler { get; set; } = new List<HacimSegmenti>();
        public List<BrucknerNoktasi> BrucknerVerisi { get; set; } = new List<BrucknerNoktasi>();
    }

    public class HacimSegmenti
    {
        public double Istasyon1 { get; set; }
        public double Istasyon2 { get; set; }
        public double Alan1 { get; set; }
        public double Alan2 { get; set; }
        public double Hacim { get; set; }
        public double Mesafe { get; set; }
    }
}
