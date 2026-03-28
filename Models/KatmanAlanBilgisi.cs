using System.Collections.Generic;

namespace Metraj.Models
{
    public class KatmanAlanBilgisi
    {
        public string MalzemeAdi { get; set; }
        public MalzemeKategorisi Kategori { get; set; }
        public double Alan { get; set; }              // m²
        public string KaynakLayerAdi { get; set; }
        public AlanTipi Tip { get; set; }
        public List<double[]> TiklamaNoktalari { get; set; } = new List<double[]>();
        public bool NesnedenSecildi { get; set; }
        public long NesneHandle { get; set; }
    }
}
