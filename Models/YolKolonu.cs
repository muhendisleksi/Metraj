using System.Collections.Generic;

namespace Metraj.Models
{
    public class YolKolonu
    {
        public string KolonHarfi { get; set; }
        public string Aciklama { get; set; }
        public List<YolKesitVerisi> Istasyonlar { get; set; } = new List<YolKesitVerisi>();
        public YolKubajSonucu KubajSonucu { get; set; }
    }
}
