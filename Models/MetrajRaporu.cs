using System;
using System.Collections.Generic;

namespace Metraj.Models
{
    public class MetrajRaporu
    {
        public List<UzunlukOlcumu> UzunlukSonuclari { get; set; } = new List<UzunlukOlcumu>();
        public List<AlanOlcumu> AlanSonuclari { get; set; } = new List<AlanOlcumu>();
        public HacimHesapSonucu HacimSonucu { get; set; }
        public List<ToplamaOgesi> ToplamaSonuclari { get; set; } = new List<ToplamaOgesi>();
        public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    }
}
