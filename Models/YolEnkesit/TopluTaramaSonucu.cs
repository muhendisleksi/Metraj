using System;
using System.Collections.Generic;

namespace Metraj.Models.YolEnkesit
{
    public class TopluTaramaSonucu
    {
        public List<KesitGrubu> Kesitler { get; set; } = new List<KesitGrubu>();
        public ReferansKesitSablonu Sablon { get; set; }
        public DateTime TaramaTarihi { get; set; }
        public int ToplamKesit { get; set; }
        public int OnayliKesit { get; set; }
        public int UyariKesit { get; set; }
        public int SorunluKesit { get; set; }
    }
}
