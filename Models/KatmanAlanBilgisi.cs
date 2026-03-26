using System.Collections.Generic;

namespace Metraj.Models
{
    public class KatmanAlanBilgisi
    {
        public string MalzemeAdi { get; set; }
        public MalzemeKategorisi Kategori { get; set; }
        public double Alan { get; set; }              // m\u00B2
        public string KaynakLayerAdi { get; set; }
        public AlanTipi Tip { get; set; }
        public List<double[]> TiklamaNoktalari { get; set; } = new List<double[]>(); // [x,y] koordinatlar\u0131
    }
}
